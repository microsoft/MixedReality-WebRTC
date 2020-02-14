# Config Linux for Building libwebrtc

Some platorms require the libwebrtc library to be built from a Unix-like environment, namely Android. For others it is a convenience: Mac, iOS, Linux.

## Setup your *nix environment

1. In a Unix-like environment, open a bash shell.

    If working in WSL/Ubuntu:

    You must use WSL2, available in Windows 10 builds 18917 or higher. WSL1 has a fatal bug relating to memory-mapped files. There is no workaround.

    WSL2 installation instructions: https://docs.microsoft.com/en-us/windows/wsl/wsl2-install

    If building on a Windows-accessible drive (USB or main disk), ensure the drive is mounted with "-o metadata", e.g.:

    `$> sudo mount -t drvfs D: /mnt/d -o metadata`
  
    This enables support for Linux-style file permissions (chown, chmod, etc).

    The filesystem must be case sensitive. There are three ways to enable this. I recommend trying all of them.
    
    The first: specify it in your `/etc/wsl.conf` file:

    ```
    sudo nano /etc/wsl.conf
    ## file contents should include this:
    [automount]
    options = "metadata,case=force"
    ```

    This approach may fail, but is the recommended solution on the web. Remaining two options:

    2. From the windows filesystem, use `fsutil.exe file setCaseSensitiveInfo`

    3. From the Linux side, try `setfattr -n system.wsl_case_sensitive -v 1 <path>` (but this also fails for me with the error "Operation not supported". See https://github.com/microsoft/WSL/issues/4261)

    I recommend trying ALL of these. One might stick.

2. Install prerequisite software packages.

    `$> sudo apt-get install clang`

3. Return to README.md for next steps.
 