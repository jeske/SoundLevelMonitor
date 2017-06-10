// Copyright (C) 2017 by David W. Jeske
// Released to the Public Domain

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using CSCore.CoreAudioAPI;

namespace SoundLevelMonitor
{
    public class AudioLevelMonitor
    {
        System.Timers.Timer dispatchingTimer;
        public double interval_ms = 50;
        IDictionary<string, SampleInfo> sessionIdToInfo = new Dictionary<string, SampleInfo>();
        IDictionary<string, List<double>> sessionIdToAudioSamples = new Dictionary<string, List<double>>();
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

        public delegate void NewAudioSamplesEvent(AudioLevelMonitor monitor);
        public event NewAudioSamplesEvent NewAudioSamplesEventListeners;

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

        public struct SampleInfo
        {
            public string sessionId;
            public int pid;
            public string SessionName;
            public double[] samples;
        }


        public IDictionary<string, SampleInfo> GetActiveSamples() {
            var outputDict = new Dictionary<string, AudioLevelMonitor.SampleInfo>();
            lock (this) {
                foreach (var kvp in sessionIdToInfo) {

                    var info = kvp.Value;
                    if (sessionIdToAudioSamples.ContainsKey(info.sessionId)) {
                        info.samples = sessionIdToAudioSamples[info.sessionId].ToArray();
                        outputDict[info.sessionId] = info;
                    }
                }
            }
            return outputDict;
        }
       
        public void CheckAudioLevels() {
            lock (this) {
                var seenPids = new HashSet<string>();
                try {
                    using (var sessionManager = GetDefaultAudioSessionManager2(DataFlow.Render)) {
                                    
                        using (var sessionEnumerator = sessionManager.GetSessionEnumerator()) {
                            foreach (var session in sessionEnumerator) {
                                using (var audioSessionControl2 = session.QueryInterface<AudioSessionControl2>()) {
                                    var process = audioSessionControl2.Process;

                                    string sessionid = audioSessionControl2.SessionIdentifier;
                                    int pid = audioSessionControl2.ProcessID;
                                    string name = audioSessionControl2.DisplayName;                                    
                                    if (process != null) {
                                        if (name == "") { name = process.MainWindowTitle; }
                                        if (name == "") { name = process.ProcessName; }
                                    }
                                    if (name == "") { name = "--unnamed--"; }
                                
                                    var sessionInfo = new SampleInfo();
                                    sessionInfo.sessionId = sessionid;
                                    sessionInfo.pid = pid;
                                    sessionInfo.SessionName = name;

                                    sessionIdToInfo[sessionid] = sessionInfo;
                                
                                    using (var audioMeterInformation = session.QueryInterface<AudioMeterInformation>()) {
                                        var value = audioMeterInformation.GetPeakValue();
                                        if (value != 0) {
                                            if (process != null) {
                                                seenPids.Add(sessionid);
                                                List<double> samples;
                                                if (!sessionIdToAudioSamples.TryGetValue(sessionid, out samples)) {
                                                    samples = new List<double>();
                                                    sessionIdToAudioSamples[sessionid] = samples;
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
                } catch (CoreAudioAPIException e) {
                    Console.WriteLine("AudioLevelMonitor exception: " + e.ToString());
                    return;
                }

                // before we are done, we need to add samples to anyone we didn't see
                var deleteSamplesForPids = new HashSet<string>();
                foreach (var kvp in sessionIdToAudioSamples) {
                    if (!seenPids.Contains(kvp.Key)) {
                        kvp.Value.Add(0.0);
                        truncateSamples(kvp.Value);
                        if (areSamplesEmpty(kvp.Value)) {
                            deleteSamplesForPids.Add(kvp.Key);
                        }
                    }
                }
                foreach (var sessionid in deleteSamplesForPids) {
                    sessionIdToAudioSamples.Remove(sessionid);
                }
            } // lock
            System.GC.Collect();
            NewAudioSamplesEventListeners?.Invoke(this);
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
