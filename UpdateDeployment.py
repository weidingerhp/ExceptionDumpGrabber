#!/usr/bin/python

import sys, getopt, io;
import yaml;

DUMP_DEST_DIR = '/mnt/dynatracegrabber'
DUMP_DEST_PVCLAIM = 'dynatracegrabber-pvc'
CREATE_DUMP_TOOL = '/usr/share/dotnet/shared/Microsoft.NETCore.App/3.1.11/createdump'
EXCEPTION_GRABBER_MOUNT = '/exceptiongrabber'
GRABBER_VERSION = 'v1.0.4'

def add_environments(image):
    # Add Environment if not existing yet
    if not 'env' in image.keys():
        image['env'] = []

    # Add the keys if needed
    if not [name for name in image['env'] if name['name'] == 'DT_CRASH_DUMP_DIR']:
        image['env'].insert(0, {'name': 'DT_CRASH_DUMP_DIR', 'value': DUMP_DEST_DIR})
    if not [name for name in image['env'] if name['name'] == 'DT_DUMP_EXEC']:
        image['env'].insert(0, {'name': 'DT_DUMP_EXEC', 'value': CREATE_DUMP_TOOL})
    if not [name for name in image['env'] if name['name'] == 'LD_PRELOAD']:
        image['env'].insert(0, {'name': 'LD_PRELOAD', 'value': '/opt/dynatrace/oneagent/agent/lib64/liboneagentproc.so'})
    if not [name for name in image['env'] if name['name'] == 'DOTNET_STARTUP_HOOKS']:
        image['env'].insert(0, {'name': 'DOTNET_STARTUP_HOOKS', 'value': EXCEPTION_GRABBER_MOUNT + '/ExceptionGrabber.dll'})

    # Add the volume mount for the current container
    if not 'volumeMounts' in image.keys():
        image['volumeMounts'] = []
    
    # check if volume-mount for
    for dest in [name for name in image['volumeMounts'] if name['mountPath'] == DUMP_DEST_DIR]:
        if 'readOnly' in dest:
            dest['readOnly'] = False
    
    if not [name for name in image['volumeMounts'] if name['mountPath'] == EXCEPTION_GRABBER_MOUNT]:
        image['volumeMounts'].insert(0, {'name': 'exceptiongrabber', 'mountPath': EXCEPTION_GRABBER_MOUNT})

    # for creating dumps we need SYS_PTRACE enabled
    if not 'securityContext' in image.keys():
        image['securityContext'] = {}
    if not 'capabilities' in image['securityContext'].keys():
        image['securityContext']['capabilities'] = {}
    if not 'add' in image['securityContext']['capabilities'].keys():
        image['securityContext']['capabilities']['add'] = []

    if not [name for name in image['securityContext']['capabilities']['add'] if name == 'SYS_PTRACE']:
        image['securityContext']['capabilities']['add'].insert(0, 'SYS_PTRACE')

def add_init_container(spec):
    # add initContainers-Section if needed
    if not 'initContainers' in spec:
        spec['initContainers'] = []

    # add exceptionGrabber init-container if not there
    if not [name for name in spec['initContainers'] if name['name'] == 'grabber']:
        spec['initContainers'].insert(0, {
            'name': 'grabber', 
            'image': 'alpine:3.12.3',
            'command': ['/bin/sh'],
            'args': ['-c', 'ARCHIVE=$(mktemp) && wget -O $ARCHIVE "https://github.com/weidingerhp/ExceptionDumpGrabber/releases/download/' + GRABBER_VERSION + '/ExceptionGrabber-Artifact.zip" && unzip -o -d ' + EXCEPTION_GRABBER_MOUNT + ' $ARCHIVE && rm -f $ARCHIVE'],
            'volumeMounts': [{'name': 'exceptiongrabber', 'mountPath': EXCEPTION_GRABBER_MOUNT}]})

    # add volumes-Section if needed
    if not 'volumes' in spec:
        spec['volumes'] = []

    # add exceptiongrabber-volume if not already there
    if not [name for name in spec['volumes'] if name['name'] == 'exceptiongrabber']:
        spec['volumes'].insert(0, {'name': 'exceptiongrabber', 'emptyDir': {} })

def add_pvc_as_volume(spec, image):
    if not [name for name in image['volumeMounts'] if name['mountPath'] == DUMP_DEST_DIR]:
        print(f'The output directory {DUMP_DEST_DIR} is not available in the deployment descriptor. Available are:')
        for name in image['volumeMounts']:
            print('- {name}'.format(name=name['mountPath']))
        print(f'will add Persistant Volume claim {DUMP_DEST_PVCLAIM} as this volume')

        if not [vol for vol in spec['volumes'] if vol['name'] == DUMP_DEST_PVCLAIM]:
            spec['volumes'].insert(0, {'name': DUMP_DEST_PVCLAIM, 'persistentVolumeClaim': {'claimName': DUMP_DEST_PVCLAIM}})

        image['volumeMounts'].insert(0, {'name': DUMP_DEST_PVCLAIM, 'mountPath': DUMP_DEST_DIR})

def update_yaml(myYaml):
    spec = myYaml['spec']['template']['spec']
    add_init_container(spec)

    for image in myYaml['spec']['template']['spec']['containers']:
        print('updating container \"' + image['image'] + "\"")
        add_environments(image)
        add_pvc_as_volume(spec, image)


    # print(myYaml['spec']['template']['spec']['containers'][0]['env'])
    return myYaml

def print_usage():
    print('Usage:\n' + sys.argv[0] + ' [-h|--help] [-i|--input]=<inputfile> [-o|--output]=<outputfile>')


argv = sys.argv[1:]
try:
    opts,args = getopt.getopt(argv, shortopts="hi:o:",longopts=["help", "input", "output"])
except getopt.GetoptError:
    print_usage()
    sys.exit(2)

for opt, arg in opts:
    if opt in ("-h", "--help"):
        print_usage()
        sys.exit()
    elif opt in ("-i", "--input"):
        iFile = arg
    elif opt in ("-o", "--output"):
        oFile = arg

try:
    with open(iFile, 'r') as inputFile:
        myYaml = yaml.load(inputFile, Loader=yaml.SafeLoader)

        with open(oFile, 'w') as outputFile:
            yaml.dump(update_yaml(myYaml), outputFile)
# except NameError:
#     print_usage()
#     sys.exit(3)
except FileNotFoundError:
    print('Input file not found\n')
    print_usage()
    sys.exit(3)
