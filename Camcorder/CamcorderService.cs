using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Timers;

namespace Camcorder {
    public partial class CamcorderService : ServiceBase {

        public const string ConfigLocation = @"C:\Program Files\Camcorder\config.json";
        public Config AppConfig;
        public List<(CameraConfig config, Process process, int id, int restartCount)> RecorderProcesses;
        public Timer WatchdogTimer, CleanupTimer;

        public CamcorderService() {
            InitializeComponent();
        }

        protected override void OnStart(string[] args) {
            EventLog.WriteEntry("Camcorder service started.");

            // open config
            if (File.Exists(ConfigLocation))
                AppConfig = Config.FromFile(ConfigLocation);
            else {
                var defaultConfig = new Config() {
                    VideoLocation = @"C:\Data\Video\",
                    Cameras = new[] {
                        new CameraConfig {
                            ClipMinutes = 15,
                            FirstCamera = 1,
                            LastCamera = 4,
                            Name = "Test Camera",
                            RetentionDays = 60,
                            UrlFormat = "http://127.0.0.1/camera-stream.mp4?camera={0}"
                        }
                    }
                };
                defaultConfig.ToFile(ConfigLocation);
                EventLog.WriteEntry($"Failed to open configuration file at {ConfigLocation}",
                    EventLogEntryType.Error);
                this.ExitCode = 1;
                Stop();
            }

            // start processes for cameras
            foreach (CameraConfig cfg in AppConfig.Cameras) {
                for (int i = cfg.FirstCamera; i <= cfg.LastCamera; i++) {
                    // check to see if directories exist
                    string videoPath = $"{AppConfig.VideoLocation}\\{cfg.Name}\\{cfg.Name}-{i}\\{cfg.Name}";

                    if (!Directory.Exists(videoPath))
                        Directory.CreateDirectory(videoPath);

                    var process = Process.Start(new ProcessStartInfo {
                        // TODO: config option for this? or just require it be in the same directory
                        FileName = "ffmpeg.exe",
                        Arguments = $"-rtsp_transport tcp -i {String.Format(cfg.UrlFormat, i)} -c copy -timeout 10" +
                                    $" -stimeout 5000000 -f segment -segment_time {cfg.ClipMinutes} -segment_format mp4" +
                                    $"-reset_timestamps 1 -strftime 1 -strftime_mkdir 1 {AppConfig.VideoLocation}\\{cfg.Name}\\{cfg.Name}-{i}\\{cfg.Name}-{i}-%Y%m%d-%H%M%S.mp4",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true
                    });

                    if (process == null) {
                        EventLog.WriteEntry($"Failed to start ffmpeg for {cfg.Name}, index {i}.", EventLogEntryType.Error);
                        continue;
                    }

                    process.EnableRaisingEvents = true;
                    process.Exited += ProcessWatchdog;

                    RecorderProcesses.Add((cfg, process, i, 0));
                }
            }

            WatchdogTimer = new Timer(5000);
            WatchdogTimer.Elapsed += ProcessWatchdog;
            WatchdogTimer.Start();

            CleanupTimer = new Timer(60 * 60 * 1000);
            CleanupTimer.Elapsed += Cleanup;
            CleanupTimer.Start();
        }

        protected void ProcessWatchdog(object sender, EventArgs e) {
            for (int i = 0; i < RecorderProcesses.Count; i++) {
                var camera = RecorderProcesses[i];
                // restart all processes which have exited
                camera.process.Refresh();
                if (camera.process.HasExited && camera.restartCount < 50) {
                    EventLog.WriteEntry($"Process for camera {camera.config.Name} #{camera.id} has died. Restarting.");
                    camera.process.Start();
                    camera.restartCount++;
                } else {
                    if (camera.restartCount <= 0) continue;
                    camera.restartCount--;
                    RecorderProcesses[i] = camera;
                }
            }
        }


        protected void Cleanup(object sender, EventArgs e) {
            foreach (var camera in RecorderProcesses) {
                string videoPath =
                    $"{AppConfig.VideoLocation}\\{camera.config.Name}\\{camera.config.Name}-{camera.id}\\";
                var deleteTime = DateTime.Now - new TimeSpan(camera.config.RetentionDays, 0, 0, 0);
                foreach (var file in Directory.EnumerateFiles(videoPath)) {
                    if (File.GetCreationTime(file) < deleteTime)
                        File.Delete(file);
                }
            }
        }


        protected override void OnStop() {
            WatchdogTimer.Stop();
            CleanupTimer.Stop();
            foreach (var camera in RecorderProcesses) {
                camera.process.Refresh();
                if (!camera.process.HasExited) {
                    // see Camcorder.CtrlCSender
                    camera.process.GracefulExit();
                }
            }
        }
    }
}
