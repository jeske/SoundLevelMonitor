// Copyright (C) 2017 by David W. Jeske
// Released to the Public Domain

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using CSCore.CoreAudioAPI;

namespace SoundManager
{
    public class AudioLevelMonitor
    {
        System.Timers.Timer dispatchingTimer;
        public double interval_ms = 100;
        IDictionary<int,string> pidToWindowTitle = new Dictionary<int,string>();
        IDictionary<int,List<double>> pidToAudioSamples = new Dictionary<int,List<double>>();
        int maxSamplesToKeep = 1000;

        public AudioLevelMonitor() {
            dispatchingTimer = new System.Timers.Timer(interval_ms);
            dispatchingTimer.Elapsed += DispatchingTimer_Elapsed;
            dispatchingTimer.AutoReset = false;
            dispatchingTimer.Start();
        }
        public void Stop() {
            dispatchingTimer.Stop();
        }

        private void DispatchingTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            this.CheckAudioLevels();
            dispatchingTimer.Start(); // trigger next timer
        }
        private void truncateSamples(List<double> samples) {
            int excessSamples = samples.Count - maxSamplesToKeep;
            while (excessSamples-- > 0) {
                samples.RemoveAt(0);
            }
        }
        private bool areSamplesEmpty(List<double> samples) {
            foreach (var val in samples) {
                if (val != 0.0) {
                    return false;
                }
            }
            return true;
        }

        public struct SampleInfo {
            public int pid;
            public string WindowTitle;
            public double[] samples;
        }


        public IDictionary<int,SampleInfo> GetActiveSamples() {
            var outputDict = new Dictionary<int, AudioLevelMonitor.SampleInfo>();
            lock (this) {                                
                foreach (var kvp in pidToAudioSamples) {
                    var info = new SampleInfo();
                    info.pid = kvp.Key;                   
                    info.WindowTitle = pidToWindowTitle[kvp.Key];
                    info.samples = pidToAudioSamples[kvp.Key].ToArray();
                    outputDict[info.pid] = info;
                }
            }
            return outputDict;
        }

        public void CheckAudioLevels() {
            lock (this) {
                var seenPids = new HashSet<int>();
                using (var sessionManager = GetDefaultAudioSessionManager2(DataFlow.Render))
                {
                    using (var sessionEnumerator = sessionManager.GetSessionEnumerator())
                    {
                        foreach (var session in sessionEnumerator)
                        {
                            using (var audioSessionControl2 = session.QueryInterface<AudioSessionControl2>()) {
                                var process = audioSessionControl2.Process;

                                int pid = audioSessionControl2.ProcessID;
                                var windowTitle = process.ProcessName;
                                if (windowTitle != null && windowTitle != "") {
                                    pidToWindowTitle[pid] = windowTitle;
                                }
                                else {
                                    if (!pidToWindowTitle.ContainsKey(pid)) {
                                        pidToWindowTitle[pid] = "--unnamed--";
                                    }
                                }

                                using (var audioMeterInformation = session.QueryInterface<AudioMeterInformation>())
                                {
                                    var value = audioMeterInformation.GetPeakValue();
                                    if (value != 0) {
                                        if (process != null) {
                                            seenPids.Add(pid);
                                            List<double> samples;
                                            if (!pidToAudioSamples.TryGetValue(pid,out samples)) {
                                                samples = new List<double>();
                                                pidToAudioSamples[pid] = samples;
                                            }
                                            var val = audioMeterInformation.GetPeakValue();
                                            samples.Add(val);
                                            truncateSamples(samples);
                                            /* Console.WriteLine("{0} {1} {2} {3}",
                                                audioSessionControl2.ProcessID,
                                                process.ProcessName,
                                                process.MainWindowTitle,
                                                val);
                                                */
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                // before we are done, we need to add samples to anyone we didn't see
                var deleteSamplesForPids = new HashSet<int>();
                foreach (var kvp in pidToAudioSamples) {
                    if (!seenPids.Contains(kvp.Key)) {
                        kvp.Value.Add(0.0);
                        truncateSamples(kvp.Value);
                        if (areSamplesEmpty(kvp.Value)) {
                            deleteSamplesForPids.Add(kvp.Key);
                        }
                    }
                }
                foreach (var pid in deleteSamplesForPids) {
                    pidToAudioSamples.Remove(pid);
                } 
            } // lock
        }

        private static AudioSessionManager2 GetDefaultAudioSessionManager2(DataFlow dataFlow) {

            using (var enumerator = new MMDeviceEnumerator()) {
                using (var device = enumerator.GetDefaultAudioEndpoint(dataFlow, Role.Multimedia)) {
                    // Console.WriteLine("DefaultDevice: " + device.FriendlyName);
                    var sessionManager = AudioSessionManager2.FromMMDevice(device);
                    return sessionManager;
                }
            }
        }

    }
}
