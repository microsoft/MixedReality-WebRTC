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

def print_warning(s):
    print(colored('warning: ', 'yellow') + s)

def print_error(s):
    print(colored('error: ', 'red') + s)

def print_step(s):
    print(colored('step: ', 'blue') + s)

def run_command(cmd, cwd=None):
    print(colored('run: ', 'magenta') + cmd)
    ret = subprocess.call(cmd.split(), shell=True, cwd=cwd)
    if ret != 0:
        print('Command failed with exit code #%d : %s' % (ret, cmd))
        sys.exit(ret)

class BuildTriple:
    def __init__(self, target, cpu, config):
        self.target = target
        self.cpu = cpu
        self.is_debug = (config == 'debug')
        self.config = config

    def __str__(self):
        return "%s-%s-%s" % (self.target, self.cpu, self.config)

class Build:
    TARGETS = ('android', 'win', 'winuwp')
    CPUS = ('x86', 'x64', 'arm', 'arm64')
    CONFIGS = ('debug', 'release')

    # Create a build with multiple variants
    def __init__(self, targets, cpus, configs, quiet):
        self.quiet = quiet
        triples = [BuildTriple(t, c, cfg) for t in targets for c in cpus for cfg in configs]
        self.build_triples = []
        ignored_triples = []
        for tri in triples:
            try:
                Build.ensure_valid_platform(tri.target, tri.cpu, tri.config)
                self.build_triples.append(tri)
            except Exception as ex:
                ignored_triples.append((tri, ex.args[0]))
                pass
        targets = ','.join(set([tri.target for tri in self.build_triples]))
        cpus = ','.join(set([tri.cpu for tri in self.build_triples]))
        configs = ','.join(set([tri.config for tri in self.build_triples]))
        print("Building MixedReality-WebRTC\n  targets : %s\n     cpus : %s\n  configs : %s"
            % (cyan(targets), cyan(cpus), cyan(configs)))
        num_variants = len(self.build_triples)
        print("Building %d variants:" % num_variants)
        for tri in self.build_triples:
            print("- %s" % str(tri))
        for (tri, msg) in ignored_triples:
            print_warning("Ignoring unsupported variant '%s': %s" % (colored(str(tri), 'white'), msg))
        if (not quiet) and (num_variants >= 2):
            choice = input("Start building %d variants (this may take some time)? [Y/n] " % num_variants).lower()
            if (choice == 'n'):
                print("Aborted by user.")
                sys.exit(0)

    @staticmethod
    def ensure_valid_platform(target, cpu, config):
        if not (target in Build.TARGETS):
            raise Exception("Invalid platform target {target}")
        if not (cpu in Build.CPUS):
            raise Exception("Invalid CPU architecture {cpu}")
        if not (config in Build.CONFIGS):
            raise Exception("Invalid build configuration {config}")
        if ((target == 'android') and (cpu != 'arm64')):
            raise Exception("Android target only supports ARM64 architecture (-c arm64).")
        if ((target == 'win') and not (cpu in ('x86', 'x64'))):
            raise Exception("Windows Desktop target only supports x86 and x64 architectures (-c [x86|x64]).")

    def checkout(self):
        # Create checkout folder
        os.makedirs(self.webrtc_dir) # mkdir -p

        # Fetch the repository
        fetch_target = 'webrtc_android' if target == 'android' else 'webrtc'
        run_command("fetch --nohooks " + fetch_target, cwd=self.webrtc_dir)

        # Sync modules
        run_command("gclient sync -D -r branch-heads/4147", cwd=self.webrtc_dir)

        # Apply UWP patches
        print("Applying UWP patches from WinRTC repository")
        os.environ['WEBRTCM84_ROOT'] = self.webrtc_src_dir
        winrtc_dir = os.path.realpath(os.path.join(self.root_dir, 'external/winrtc'))
        print("WinRTC root path : " + winrtc_dir)
        patch_path = os.path.realpath(os.path.join(winrtc_dir, 'patches_for_WebRTC_org/m84'))
        print("WinRTC patch path : " + patch_path)
        run_command("patchWebRTCM84.cmd", cwd=patch_path)

    def build(self):
        # Compute paths
        cur_dir = os.path.dirname(os.path.realpath(__file__))
        self.root_dir = os.path.realpath(os.path.join(cur_dir, '../..'))
        print("MixedReality-WebRTC repository root : %s" % self.root_dir)
        self.webrtc_dir = os.path.realpath(os.path.join(self.root_dir, 'external/libwebrtc'))
        print("libwebrtc root : %s" % self.webrtc_dir)
        self.webrtc_src_dir = os.path.realpath(os.path.join(self.webrtc_dir, 'src'))

        # Ensure depot_tools on the PATH
        ensure_depot_tools(self.root_dir)

        # Configure environment
        os.environ['DEPOT_TOOLS_WIN_TOOLCHAIN'] = '0'
        os.environ['GYP_MSVS_VERSION'] = '2019'

        # Checkout
        has_checkout = os.path.exists(self.webrtc_dir)
        if not has_checkout:
            self.checkout()
        else:
            print("Reusing existing checkout. Delete the '%s' folder to force a clean checkout." % self.webrtc_dir)

        # Build all variants
        for tri in self.build_triples:
            self.build_variant(tri)

    # Build a single triple variant
    def build_variant(self, build_triple):
        # Ensure build folder exists
        webrtc_out_dir_rel = "out/%s/%s/%s" % (build_triple.target, build_triple.cpu, build_triple.config)
        webrtc_out_dir = os.path.realpath(os.path.join(self.webrtc_src_dir, webrtc_out_dir_rel))
        print_step("Building variant '%s' to '%s'" % (colored(str(build_triple), 'white'), webrtc_out_dir_rel))
        if not os.path.exists(webrtc_out_dir):
            os.makedirs(webrtc_out_dir) # mkdir -p

        # Write args.gn
        build_options = {
            'is_debug': 'true' if build_triple.is_debug else 'false',
            'use_lld': 'false',
            'is_clang': 'false',
            'rtc_include_tests': 'false',
            'rtc_build_examples': 'false',
            'rtc_build_tools': 'false',
            'rtc_win_video_capture_winrt': 'true',
            'rtc_win_use_mf_h264': 'true',
            'enable_libaom': 'true',
            'rtc_enable_protobuf': 'false',
            'target_os': '\"%s\"' % build_triple.target,
            'target_cpu': '\"%s\"' % build_triple.cpu
        }
        with open(os.path.realpath(os.path.join(webrtc_out_dir, 'args.gn')), 'w') as fp:
            opt_str = '\n'.join("%s=%s" % (k, v) for k,v in build_options.items())
            fp.write(opt_str)

        # Build
        run_command("gn gen %s --filters=//:webrtc" % webrtc_out_dir_rel, cwd=self.webrtc_src_dir)
        run_command("ninja -C " + webrtc_out_dir_rel, cwd=self.webrtc_src_dir)


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
    parser.add_option("-r", "--release",
                    action="store_true", dest="release", default=False,
                    help="build release variant; used with --debug to build both variants")
    parser.add_option("-q", "--quiet",
                    action="store_true", dest="quiet", default=False,
                    help="don't prompt for user validation")

    # Parse options (flags) and arguments; we do not use any argument
    (options, args) = parser.parse_args()

    # Invoke the build with the given options
    try:
        targets = options.target.split(',')
        cpus = options.cpu.split(',')
        configs = []
        if options.debug:
            configs.append('debug')
        if options.release or (not options.debug):
            configs.append('release')
        b = Build(targets, cpus, configs, options.quiet)
        b.build()
    except Exception as ex:
        msg = getattr(ex, 'message', str(ex))
        parser.error(": " + msg if len(msg) > 0 else "Unknown exception")
        sys.exit(2)

if __name__ == "__main__":
    main()
