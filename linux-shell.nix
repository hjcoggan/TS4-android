# Build environment for Top Spin 4 on aarch64 Linux (the Retroid Pocket Nova).
#
# nixpkgs is the revision nova-nix pins, so the game links against the same
# glibc, libstdc++ and Mesa the device already runs.
#
#   nix-shell linux-shell.nix
let
  nixpkgs = builtins.getFlake "github:NixOS/nixpkgs/d4f25a3e562008648d45bcf2d70bcc7422cad84e";
  pkgs = nixpkgs.legacyPackages.aarch64-linux;
in
(pkgs.mkShell.override { stdenv = pkgs.clangStdenv; }) {
  nativeBuildInputs = with pkgs; [
    cmake
    ninja
    pkg-config
    python3
    perl
    git
    wayland-scanner
    patchelf
  ];
  buildInputs = with pkgs; [
    wayland
    wayland-protocols
    libffi
    libglvnd
    libxkbcommon
    libdecor
    libx11
    libxext
    libxcursor
    libxi
    libxrandr
    libxfixes
    libxscrnsaver
    libxtst
    libxcb
    libxcb-util
    xcb-util-cursor
    libxkbfile
    dbus
    libdrm
    libgbm
    alsa-lib
    libpulseaudio
    pipewire
    libusb1
    udev
  ];
  hardeningDisable = [ "fortify" ];
}
