
# SoundLevelMonitor

by David Jeske

This is a simple tool I cooked up so I could figure out which app was responsible for annoying errant notification sounds.

There are WPF and Windows.Forms versions. 

It has turned into a bit of a WPF challenge. It's hard to match GDI's performance using WPF's retained mode drawing. It's also hard (impossible?) to achieve the same crisp single pixel lines that GDI draws in WPF.

![SoundLevelMonitor Screenshot](https://raw.githubusercontent.com/jeske/SoundLevelMonitor/master/info/SoundLevelMonitor-screenshot.png)
