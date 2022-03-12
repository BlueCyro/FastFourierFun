# Fast fourier fun

### This is a [NeosModLoader](https://github.com/zkxs/NeosModLoader) mod that lets you get FFT values out of an audio clip.



## Installation

#### Make sure NeosModLoader is installed and just drop the latest release into the nml_mods folder.

## Usage

- Simply grab a StaticAudioClip or imported audio clip with the LogiX tip and hit the "Get FFT" button on your context menu.
- It will take a little while to compute the FFT, but once it's done you will receive an item with an animation clip on it. Each track on the clip is a value from the FFT in ascending order. 

## Notes

- Note that for now the FFT result is averaged depending on how many channels the audio clip has. (E.g. 2 channels - FFT result of both channels is averaged together)
- The FFT is also represented as an absolute value, so you're not gonna get any negatives in there.
- I'd also recommend getting the absolute value of the FFT result anyways, as this may very well change in the future.
## Limitations
- This mod won't let you get FFT from a video file, it has to be a format that supports native playback. (ogg, flac, wav, etc.)
- This mod will not work on audio streams.
- *Please don't use an animator to play the clip, it will be very laggy.* I recommend only sampling it with LogiX.


## For developers
- This mod implements an extension to AudioX datatypes so you can simply call `AudioX.GetFFTAnimation(int FFTBucketSize)` to get the FFT animation clip.
- I highly recommend using this function in an asynchronous manner, as it will take a while to compute the FFT.
- Same goes for actually writing it into a StaticAnimationProvider, that takes the longest.
<p>&nbsp;</p>

- This mod was developed in vscode, not visual studio as most other mods are.