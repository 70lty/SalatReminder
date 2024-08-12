using System;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;
using NAudio.Wave;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PrayerTimesApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            ShowConsoleWindow();
            Console.WriteLine("Ne me ferme pas, je vais le faire tout seul !");

            AddApplicationToStartup();
            string url = "https://muslimsalat.com/ans.json"; // Changez l'URL pour votre location et verifier sur le site Muslim Salat, vous pouvez cherchez avec code postal, ville.
            var prayerTimes = await GetPrayerTimes(url);

            if (prayerTimes != null)
            {
                foreach (var prayer in prayerTimes)
                {
                    Console.WriteLine($"{prayer.Key}: {prayer.Value:HH:mm}");
                }

                string audioUrl = "https://media.sd.ma/assabile/adhan_3435370/0bf83c80b583.mp3";
                string audioFile = Path.Combine(Path.GetTempPath(), "adhan.mp3");

                await DownloadAudio(audioUrl, audioFile);

                SchedulePrayers(prayerTimes, audioFile);

                await Task.Delay(1000);
                HideConsoleWindow();

                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            else
            {
                Console.WriteLine("Failed to retrieve prayer times.");
            }
        }

        static async Task<Dictionary<string, DateTime>> GetPrayerTimes(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    var response = await client.GetStringAsync(url);
                    dynamic data = JsonConvert.DeserializeObject(response);

                    var items = data.items[0];
                    var prayerTimes = new Dictionary<string, DateTime>
                    {
                        { "Fajr", DateTime.Parse((string)items.fajr) },
                        { "Dhuhr", DateTime.Parse((string)items.dhuhr) },
                        { "Asr", DateTime.Parse((string)items.asr) },
                        { "Maghrib", DateTime.Parse((string)items.maghrib) },
                        { "Isha", DateTime.Parse((string)items.isha) },
                        { "Test", DateTime.Today.AddHours(23).AddMinutes(40) }
                    };

                    return prayerTimes;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    return null;
                }
            }
        }

        static async Task DownloadAudio(string url, string filename)
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }
            };

            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    var data = await client.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(filename, data);
                    Console.WriteLine($"Downloaded audio to {filename}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to download audio. Error: {ex.Message}");
                }
            }
        }

        static void SchedulePrayers(Dictionary<string, DateTime> prayerTimes, string audioFile)
        {
            foreach (var prayer in prayerTimes)
            {
                TimeSpan timeToPrayer = prayer.Value - DateTime.Now;
                if (timeToPrayer.TotalMilliseconds > 0)
                {
                    Console.WriteLine($"Scheduling {prayer.Key} in {timeToPrayer.TotalMinutes} minutes.");
                    Task.Delay(timeToPrayer).ContinueWith(_ => PlayAdhan(audioFile));
                }
            }
        }

        static void PlayAdhan(string audioFile)
        {
            using (var audioFileReader = new AudioFileReader(audioFile))
            using (var outputDevice = new WaveOutEvent())
            {
                outputDevice.Init(audioFileReader);
                outputDevice.Play();

                // Supprimez ces 2 lignes si vous voulez entendre l'Adhan en entier !
                Thread.Sleep(5000);
                outputDevice.Stop();
            }

            Console.WriteLine("Played Adhan.");
        }

        static void AddApplicationToStartup()
        {
            try
            {
                string appName = "Salat Reminder";
                string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

                RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (registryKey != null)
                {
                    var currentValue = registryKey.GetValue(appName);
                    if (currentValue == null || currentValue.ToString() != appPath)
                    {
                        registryKey.SetValue(appName, appPath);
                        Console.WriteLine("L'application a été ajoutée à la liste de démarrage.");
                    }
                    else
                    {
                        Console.WriteLine("L'application est déjà dans la liste de démarrage.");
                    }

                    CheckStartupStatus(appName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'ajout à la liste de démarrage: {ex.Message}");
            }
        }

        static void CheckStartupStatus(string appName)
        {
            try
            {
                string keyPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
                using (RegistryKey startupStatusKey = Registry.CurrentUser.OpenSubKey(keyPath, false))
                {
                    if (startupStatusKey != null)
                    {
                        var statusValue = startupStatusKey.GetValue(appName);
                        if (statusValue != null && ((byte[])statusValue)[0] == 0x02)
                        {
                            Console.WriteLine("L'application est désactivée dans le gestionnaire des tâches. Veuillez l'activer manuellement.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la vérification du statut de démarrage: {ex.Message}");
            }
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        static void HideConsoleWindow()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
        }

        static void ShowConsoleWindow()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_SHOW);
        }
    }
}