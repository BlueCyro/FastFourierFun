# Fast fourier fun

# DISCLAIMER: This is a work in progress. It is not yet ready for production use. Any and all implemention of features by this mod are subject to change at any time without warning.

### That being said; this is a [NeosModLoader](https://github.com/zkxs/NeosModLoader) mod that lets you get FFT values out of an audio clip.



## Installation

#### Make sure NeosModLoader is installed and just drop the latest release into the nml_mods folder.

## Usage

- Simply grab a StaticAudioClip or imported audio clip with the LogiX tip and hit the "Get FFT" button on your context menu.
- It will take a little while to compute the FFT, but once it's done you will receive an item with an animation clip on it. Each track on the clip is a value from the FFT in ascending order. 

## Notes

- Note that for now the FFT result is the maximum amplitude of the results.  (E.g. 2 channels - per FFT slice, the result is the maximum of the channels' amplitudes.)
- The FFT is also represented as an absolute value, so you're not gonna get any negatives in there.
- I'd also recommend getting the absolute value of the FFT result anyways, as this may very well change in the future.
## Limitations
- This mod won't let you get FFT from a video file, it has to be a format that supports native playback. (ogg, flac, wav, etc.)
- This mod will not work on audio streams.
- *Please don't use an animator to play the clip, it will be very laggy.* I recommend only sampling it with LogiX.


## For developers
- This mod implements an extension to AudioX datatypes so you can simply call:
- `AudioX.GetFFTAnimation(int FFTBucketSize)` to get the FFT animation clip. (This must be a power of 2)
- I highly recommend using this function in an asynchronous manner, as it will take a while to compute the FFT.
- Same goes for actually writing it into a StaticAnimationProvider, that takes the longest.
<p>&nbsp;</p>

- This mod was developed in vscode, not visual studio as most other mods are.
