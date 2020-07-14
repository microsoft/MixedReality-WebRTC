#!/usr/bin/env python
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

from optparse import OptionParser
import sys
import os
import subprocess

def colored(s, col):
    RESET = "\x1b[0m"
    color_dict = {
        'black': 30,
        'red': 31,
        'green': 32,
        'yellow': 33,
        'blue': 34,
        'magenta': 35,
        'cyan': 36,
        'white': 37
    }
    fg_col = "\x1b[1;%dm" % color_dict[col]
    return fg_col + s + RESET

def cyan(s):
    return colored(s, 'cyan')

def ensure_depot_tools(root_dir):
    depot_tools_dir = os.path.realpath(os.path.join(root_dir, 'external/depot_tools'))
    depot_tools_dir = os.path.normpath(os.path.normcase(depot_tools_dir))
    found = False
    dirs = os.getenv('PATH').split(os.pathsep)
    for path in dirs:
        if os.path.normpath(os.path.normcase(path)) == depot_tools_dir:
            found = True
            break
    if not found:
        print("Adding depot_tools folder to PATH : " + depot_tools_dir)
        dirs.insert(0, depot_tools_dir)
        os.environ['PATH'] = os.pathsep.join(dirs)

def run_command(cmd, cwd=None):
    print(colored('run: ', 'magenta') + cmd)
    ret = subprocess.call(cmd.split(), shell=True, cwd=cwd)
    if ret != 0:
        print('Command failed with exit code #%d : %s' % (ret, cmd))
        sys.exit(ret)

def build(target, cpu, is_debug):
    # Print input arguments
    config = 'debug' if is_debug else 'release'
    print("Building MixedReality-WebRTC\n  target : %s\n     cpu : %s\n  config : %s"
        % (cyan(target), cyan(cpu), cyan(config)))

    # Prepare cloning folder
    cur_dir = os.path.dirname(os.path.realpath(__file__))
    root_dir = os.path.realpath(os.path.join(cur_dir, '../..'))
    print("    root : %s" % root_dir)
    webrtc_dir = os.path.realpath(os.path.join(root_dir, 'external/libwebrtc'))
    print("  webrtc : %s" % webrtc_dir)
    webrtc_src_dir = os.path.realpath(os.path.join(webrtc_dir, 'src'))
    has_checkout = os.path.exists(webrtc_dir)
    if not has_checkout:
        os.makedirs(webrtc_dir) # mkdir -p

    # Ensure depot_tools on the PATH
    ensure_depot_tools(root_dir)

    # Configure environment
    os.environ['DEPOT_TOOLS_WIN_TOOLCHAIN'] = '0'
    os.environ['GYP_MSVS_VERSION'] = '2019'

    # Checkout
    if has_checkout:
        print("Reusing existing checkout. Delete the '%s' folder to force a clean checkout." % webrtc_dir)
    else:
        # Fetch the repository
        fetch_target = 'webrtc_android' if target == 'android' else 'webrtc'
        run_command("fetch --nohooks " + fetch_target, cwd=webrtc_dir)

        # Sync modules
        run_command("gclient sync -D -r branch-heads/4147", cwd=webrtc_dir)

        # Apply UWP patches
        if target == 'winuwp':
            print("Applying UWP patches from WinRTC repository")
            os.environ['WEBRTCM84_ROOT'] = webrtc_src_dir
            winrtc_dir = os.path.realpath(os.path.join(root_dir, 'external/winrtc'))
            print("WinRTC root path : " + winrtc_dir)
            patch_path = os.path.realpath(os.path.join(winrtc_dir, 'patches_for_WebRTC_org/m84'))
            print("WinRTC patch path : " + patch_path)
            run_command("patchWebRTCM84.cmd", cwd=patch_path)

    # Ensure build folder exists
    webrtc_out_dir_rel = "out/%s/%s/%s" % (target, cpu, config)
    webrtc_out_dir = os.path.realpath(os.path.join(webrtc_src_dir, webrtc_out_dir_rel))
    if not os.path.exists(webrtc_out_dir):
        os.makedirs(webrtc_out_dir) # mkdir -p

    # Write args.gn
    build_options = {
        'is_debug': 'true' if is_debug else 'false',
        'use_lld': 'false',
        'is_clang': 'false',
        'rtc_include_tests': 'false',
        'rtc_build_examples': 'false',
        'rtc_build_tools': 'false',
        'rtc_win_video_capture_winrt': 'true',
        'rtc_win_use_mf_h264': 'true',
        'enable_libaom': 'true',
        'rtc_enable_protobuf': 'false',
        'target_os': '\"%s\"' % target
    }
    with open(os.path.realpath(os.path.join(webrtc_out_dir, 'args.gn')), 'wb') as fp:
        opt_str = '\n'.join("%s=%s" % (k, v) for k,v in build_options.items())
        fp.write(opt_str)

    # Build
    run_command("gn gen %s --filters=//:webrtc" % webrtc_out_dir_rel, cwd=webrtc_src_dir)
    run_command("ninja -C " + webrtc_out_dir_rel, cwd=webrtc_src_dir)

def main():
    # Configure option parser
    parser = OptionParser()
    parser.add_option("-v", "--verbose",
                    action="store_true", dest="verbose", default=True,
                    help="make lots of noise [default]")
    parser.add_option("-t", "--target",
                    help="target platform: android, win, winuwp")
    parser.add_option("-c", "--cpu",
                    help="cpu: x86, x64, arm, arm64")
    parser.add_option("-d", "--debug",
                    action="store_true", dest="debug", default=False,
                    help="build debug variant")

    # Parse options (flags) and arguments; we do not use any argument
    (options, args) = parser.parse_args()
    if not (options.target in ('android', 'win', 'winuwp')):
        parser.error("Unknown target platform '%s'" % options.target)
        sys.exit(2)
    if not (options.cpu in ('x86', 'x64', 'arm', 'arm64')):
        parser.error("Unknown cpu '%s'" % options.config)
        sys.exit(2)

    # Invoke the build with the given options
    build(options.target, options.cpu, options.debug)

if __name__ == "__main__":
    main()
