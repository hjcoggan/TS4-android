# Classic renderer first, then Native renderer; Vulkan only

The Classic renderer comes first, to reach playable, and it stays as the reference renderer. The Native renderer, ported from WWE All Stars, follows and owns render targets, which resolution scaling and MSAA need: upscaling crashes the emulation-style path in Xenia. Both renderers stay in the build side by side. D3D12 is not supported. SVR2011 hit D3D12 device hangs, Proton runs Vulkan natively for Steam Deck, and a second backend would double the test matrix for a solo developer.
