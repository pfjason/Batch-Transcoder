## Batch Folder Transcoder

This is a small tool I created for myself for post-processing of downloaded videos while trying to reduce the disk size of my current video collection.
By default it will scan a directory for media files using the MediaInfo library, and transcode all video files it finds to H.265 (HEVC) / Matroska using FFmpeg, and copy/move any non-media files so that thumbnails, subtitles, and other ancillary files like a transcoding RoboCopy. Additionally, it will scan the metadata of the video files to determine if it was generated by PlayOn, and if so, by default will remove the commercials and the PlayOn banner from the beginning and end. Any of those steps can be disabled using command line parameters.

```
Usage:

BatchTranscoder.exe [Input Folder] [Output Folder] [Options]

     -CRF [##]              CRF Transcode Quality (0-51)
     -PRESET [PRESET]       x265 Transcode Preset (placebo - ultrafast)
     -DELETE                Deletes source file upon successful transcode.
     -NOTRANSCODE           Skips H.265 Transcode step in process
     -NOADREMOVAL           Skips PlayOn Detected Ad Removal
     -NOBANNERREMOVAL       Skips PlayOn Banner Removal
     -NOAUTOCROP            Disables AutoCropping
     -1-PASS                Disables 2-Pass Transcode
```
