
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.IsolatedStorage;
using System.IO;
using static System.Formats.Asn1.AsnWriter;
using System.Xml.Linq;
using System.Numerics;
using System.Linq.Expressions;
using System.Security.Cryptography.X509Certificates;

namespace guessmaker_2023
{
    //define player class
    public class Player
    {
        public string? PlayerName { get; set; }
        public int PlayerGuess{ get; set; }
        public int Guesses { get; set; }
        public int Index { get; set; }
    }

    public partial class MainWindow : Window
    {
        
        // Access class to check by player and guessed number
        private List<Player> players;
        private int secretNumber = 0;
        private object lockObject = new object();

        // Used to lock threads via monitoring
        private object ln = new object();
        private BackgroundWorker bgWorker;

        public MainWindow()
        {
            InitializeComponent();
            ResetState();
            ReadFromISO();

            // Defines thread one and two, allow to work in background, then start
            Thread thread1 = new Thread(KeepTrackOfPlayer1);
            thread1.IsBackground = true;
            thread1.Start();

            Thread thread2 = new Thread(KeepTrackOfPlayer2);
            thread2.IsBackground = true;
            thread2.Start();

            Thread thread3 = new Thread(CheckGuess);
            thread3.IsBackground = true;
            thread3.Start();

            
      
            //create background worker
            bgWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };

            // get background workers to use the methods below
            bgWorker.DoWork += bgWorker_DoWork;
            bgWorker.ProgressChanged += bgWorker_ProgressChanged;
            bgWorker.RunWorkerAsync();
        }

        private void UpdatePlayerNameAndScore(int playerNum)
        {
            // Update name
            lock (ln)
            {
                string name = (playerNum == 1) ? player_1_nameBox.Text : player_2_nameBox.Text;
                players[playerNum - 1].PlayerName = name;
            }

            // Get guess
            string guessText = (playerNum == 1) ? guess_1_numberBox.Text : guess_2_numberBox.Text;
            int guess = 0;
            try
            {
                guess = guessText.Trim() == "" ? 0 : int.Parse(guessText);
            } catch (Exception ex)
            {
                guess = 0;
            }

            // If guess > 0 and different from current guess
            var currentGuess = players[playerNum - 1].PlayerGuess;
            if (guess > 0 && currentGuess != guess && players[playerNum - 1].Guesses < 3)
            {
                lock (ln)
                {
                    players[playerNum - 1].Guesses += 1;
                    players[playerNum - 1].PlayerGuess = guess;
                    if (playerNum == 1)
                    {
                        guess_1_numberBox.Text = "";
                    } else
                    {
                        guess_2_numberBox.Text = "";
                    }
                }
            }
        }

        private void KeepTrackOfPlayer1()
        {
            try
            {
                while (true)
                {
                    Dispatcher.Invoke(() => UpdatePlayerNameAndScore(1));
                }
            } catch (Exception ex)
            {
                // Do nothing
            }
        }

        private void KeepTrackOfPlayer2()
        {
            try
            {
                while (true)
                {
                    Dispatcher.Invoke(() => UpdatePlayerNameAndScore(2));
                }
            }
            catch (Exception ex)
            {
                // Do nothing
            }
        }

        private static int randomNumber()
        {
            Random random = new Random();
            return random.Next(1, 9);
        }

        //thread 4
        [STAThread]
        static void SaveToISO(string result)
        {
            IsolatedStorageFile isoFile =
            IsolatedStorageFile.GetUserStoreForDomain();
            try
            {
                // write data to the stream
                IsolatedStorageFileStream isoStream =
                new IsolatedStorageFileStream(
                "GameResults.txt", FileMode.Append,
                FileAccess.Write, isoFile);
                try
                {
                    StreamWriter writer = new StreamWriter(isoStream);
                    try
                    {
                        writer.WriteLine(result);
                    }
                    finally
                    {
                        writer.Close();
                    }
                }
                finally
                {
                    isoStream.Close();
                }
            }
            catch (Exception ex)
            {
                //Do Nothing
            }
        }
        public void ReadFromISO()
        {
            IsolatedStorageFile isoFile =
            IsolatedStorageFile.GetUserStoreForDomain();
            IsolatedStorageFileStream isoStream = new IsolatedStorageFileStream("GameResults.txt",
            FileMode.Open, FileAccess.Read, isoFile);
            StreamReader sr = new StreamReader(isoStream);
            try
            {
                string winners;
                while ((winners = sr.ReadLine()) != null)
                {
                    DisplayWinners.Items.Add(winners);
                }
            }
            catch(Exception ex)
            {
                //doo nothing
            }
        }

        //check guesses

           public void CheckGuess() 
        {
            string results = "";
            try
            {
                while (true)
                {
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var player in players)
                        {
                            if (player.PlayerGuess == secretNumber)
                            {
                                MessageBox.Show("Great job " + player.PlayerName + "! The number was " + secretNumber);
                                results =   player.PlayerName + " Won! the secret number was " + secretNumber + " ... Won in " + player.Guesses + " guesses!";
                                SaveToISO(results);
                                ResetState();
                                return;
                            }
                        }
                        if (players[0].Guesses == 3 && players[1].Guesses == 3)
                        {
                            MessageBox.Show("Game over, the number was " + secretNumber);
                            results =  "nobody won.. the number was: " +secretNumber ;
                            SaveToISO(results);
                            ResetState();

                        }
                    });
                }

            }

            catch (Exception ex)
            {
                // Do nothing
            }
            
        }
        
        private void bgWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState != null)
            {
                var player = (Player)e.UserState!;
                
                // Update list with current guess
                var text = player.PlayerName + " Guessed: " + player.PlayerGuess + " (" + (3 - player.Guesses) + " guesses left)";
                if (DisplayGuessesList.Items.Count < 2)
                {
                    DisplayGuessesList.Items.Add("-");
                    DisplayGuessesList.Items.Add("-");
                }
                DisplayGuessesList.Items[player.Index] = text;

                // Update progress bar
                if (player.Index == 0)
                {
                    progressBar.Value = e.ProgressPercentage;
                } else
                {
                    progressBar_2.Value = e.ProgressPercentage;
                }
            }
        }

        private void bgWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                Monitor.Enter(ln);
                try
                {
                    foreach (var player in players)
                    {
                        // get progress percentage
                        var progress = 100;
                        switch (player.Guesses)
                        {
                            case (1):
                                progress = 66;
                                break;
                            case (2):
                                progress = 33;
                                break;
                            case (3):
                                progress = 0;
                                break;
                            default:
                                progress = 100;
                                break;
                        }
                        
                        // update progress bar, and name at once
                        bgWorker.ReportProgress(progress, player);
                    }
                    bgWorker.ReportProgress(0);
                }
                finally
                {
                    Monitor.Exit(ln);
                }
                Thread.Sleep(50);
            }
        }

        private void ResetState()
        {
            secretNumber = randomNumber();
            DisplayGuessesList.Items.Clear();
            guess_1_numberBox.Clear();
            guess_2_numberBox.Clear();
            players = new List<Player>
            {
                new Player { PlayerName = player_1_nameBox.Text, PlayerGuess = 0, Index = 0, Guesses = 0 },
                new Player { PlayerName = player_2_nameBox.Text, PlayerGuess = 0, Index = 1, Guesses = 0 }
            };
            //clear list and update with new items
            DisplayWinners.Items.Clear();
            ReadFromISO();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ResetState();
        }
    }

}

