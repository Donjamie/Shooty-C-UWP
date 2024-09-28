using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.System;
using Windows.UI.Core;
using Windows.Storage;
using Windows.Gaming.Input;
using Windows.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.UI.Xaml.Media;
using System.Diagnostics;


namespace Shooty_C__UWP
{
    public sealed partial class MainPage : Page
    {
        private const int MaxAsteroids = 20;
        private const int MaxBullets = 50;
        private const int GameDuration = 1500; // 50 seconds (30 ticks per second)

        private List<TextBlock> asteroids = new List<TextBlock>();
        private List<TextBlock> bullets = new List<TextBlock>();
        private DispatcherTimer gameTimer;
        private Random random = new Random();

        private int hits = 0;
        private int played = 0;
        private int highScore = 0;

        private Gamepad controller;
        private GamepadReading previousReading;

        private void GameCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            AdjustScaleAndPosition();
        }

        private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustScaleAndPosition();
        }

        private void AdjustScaleAndPosition()
        {
            var screenWidth = Window.Current.Bounds.Width;
            var screenHeight = Window.Current.Bounds.Height;

            // Design dimensions (original dimensions of the game)
            double designWidth = 800;  // Change this to your design width
            double designHeight = 600; // Change this to your design height

            // Calculate the scaling factors based on the screen size
            double widthRatio = screenWidth / designWidth;
            double heightRatio = screenHeight / designHeight;
            double scale = Math.Min(widthRatio, heightRatio);

            // Ensure the scale is valid (greater than zero and finite)
            if (double.IsInfinity(scale) || double.IsNaN(scale) || scale <= 0)
            {
                scale = 1; // Default to 1 if invalid
            }

            // Apply the scale transform
            GameCanvas.RenderTransform = new ScaleTransform { ScaleX = scale, ScaleY = scale };

            // Calculate the new position for the player ship
            // Center horizontally, and keep it a certain distance from the bottom
            double playerShipWidth = PlayerShip.ActualWidth * scale;
            double playerShipHeight = PlayerShip.ActualHeight * scale;

            // Center horizontally
            double playerShipX = (screenWidth - playerShipWidth) / 2;

            // Position near the bottom of the screen with a margin
            double playerShipY = screenHeight - playerShipHeight - 20; // 20 pixels from the bottom

            // Set the new position
            Canvas.SetLeft(PlayerShip, playerShipX);
            Canvas.SetTop(PlayerShip, playerShipY);
        }

        public MainPage()
        {
           
            this.InitializeComponent();
            GameCanvas.Loaded += GameCanvas_Loaded; // Attach Loaded event
            this.SizeChanged += MainPage_SizeChanged; // Attach SizeChanged event
            this.InitializeComponent();
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Gamepad.GamepadAdded += Gamepad_GamepadAdded;
            Gamepad.GamepadRemoved += Gamepad_GamepadRemoved;
            LoadHighScore();
        }

        private void Gamepad_GamepadAdded(object sender, Gamepad e)
        {
            controller = e;
        }

        private void Gamepad_GamepadRemoved(object sender, Gamepad e)
        {
            if (controller == e)
                controller = null;
        }

        private async void LoadHighScore()
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            try
            {
                StorageFile file = await localFolder.GetFileAsync("highscore.txt");
                string scoreText = await FileIO.ReadTextAsync(file);
                highScore = int.Parse(scoreText);
                HighScoreTextBlock.Text = $"High Score: {highScore}";
            }
            catch (Exception)
            {
                highScore = 0;
            }
        }

        private async void SaveHighScore()
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile file = await localFolder.CreateFileAsync("highscore.txt", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, highScore.ToString());
        }

        private void StartGame_Click(object sender, RoutedEventArgs e)
        {
            StartScreen.Visibility = Visibility.Collapsed;
            InitializeGame();
        }

        private void InitializeGame()
        {
            hits = 0;
            played = 0;
            HitsTextBlock.Text = "Hits: 0";

            CreateAsteroids();

            gameTimer = new DispatcherTimer();
            gameTimer.Tick += GameTimer_Tick;
            gameTimer.Interval = TimeSpan.FromMilliseconds(33); // ~30 FPS
            gameTimer.Start();
        }

        private void CreateAsteroids()
        {
            for (int i = 0; i < MaxAsteroids; i++)
            {
                TextBlock asteroid = new TextBlock
                {
                    Text = "*",
                    Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White),
                    FontSize = 20
                };
                Canvas.SetLeft(asteroid, random.Next(0, (int)GameCanvas.ActualWidth));
                Canvas.SetTop(asteroid, random.Next(0, (int)GameCanvas.ActualHeight / 2));
                GameCanvas.Children.Add(asteroid);
                asteroids.Add(asteroid);
            }
        }

        private void GameTimer_Tick(object sender, object e)
        {
            played++;
            UpdateTimer();
            MoveBullets();
            CheckCollisions();
            HandleControllerInput();

            if (played >= GameDuration)
            {
                EndGame();
            }
        }

        private void HandleControllerInput()
        {
            if (controller != null)
            {
                GamepadReading reading = controller.GetCurrentReading();

                // D-pad left
                if (reading.Buttons.HasFlag(GamepadButtons.DPadLeft))
                {
                    MovePlayer(-5);
                }
                // D-pad right
                if (reading.Buttons.HasFlag(GamepadButtons.DPadRight))
                {
                    MovePlayer(5);
                }
                // A button (shoot)
                if (reading.Buttons.HasFlag(GamepadButtons.A) && !previousReading.Buttons.HasFlag(GamepadButtons.A))
                {
                    Shoot();
                }

                previousReading = reading;
            }
        }

        private void UpdateTimer()
        {
            int remainingTime = (GameDuration - played) / 30;
            TimerTextBlock.Text = $"Time: {remainingTime}s";
        }

        private void MoveBullets()
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                TextBlock bullet = bullets[i];
                double top = Canvas.GetTop(bullet);
                top -= 5;
                if (top < 0)
                {
                    GameCanvas.Children.Remove(bullet);
                    bullets.RemoveAt(i);
                }
                else
                {
                    Canvas.SetTop(bullet, top);
                }
            }
        }

        private void CheckCollisions()
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                TextBlock bullet = bullets[i];
                double bulletLeft = Canvas.GetLeft(bullet);
                double bulletTop = Canvas.GetTop(bullet);

                for (int j = asteroids.Count - 1; j >= 0; j--)
                {
                    TextBlock asteroid = asteroids[j];
                    double asteroidLeft = Canvas.GetLeft(asteroid);
                    double asteroidTop = Canvas.GetTop(asteroid);

                    if (Math.Abs(bulletLeft - asteroidLeft) < 10 &&
                        Math.Abs(bulletTop - asteroidTop) < 10)
                    {
                        GameCanvas.Children.Remove(bullet);
                        bullets.RemoveAt(i);
                        GameCanvas.Children.Remove(asteroid);
                        asteroids.RemoveAt(j);
                        CreateNewAsteroid();
                        hits++;
                        HitsTextBlock.Text = $"Hits: {hits}";
                        break;
                    }
                }
            }
        }

        private void CreateNewAsteroid()
        {
            TextBlock asteroid = new TextBlock
            {
                Text = "*",
                Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White),
                FontSize = 20
            };
            Canvas.SetLeft(asteroid, random.Next(0, (int)GameCanvas.ActualWidth));
            Canvas.SetTop(asteroid, random.Next(0, (int)GameCanvas.ActualHeight / 2));
            GameCanvas.Children.Add(asteroid);
            asteroids.Add(asteroid);
        }

        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            switch (args.VirtualKey)
            {
                case VirtualKey.Left:
                    MovePlayer(-10);
                    break;
                case VirtualKey.Right:
                    MovePlayer(10);
                    break;
                case VirtualKey.Space:
                    Shoot();
                    break;
            }
        }

        private void MovePlayer(int offset)
        {
            double left = Canvas.GetLeft(PlayerShip);
            left += offset;
            left = Math.Max(0, Math.Min(left, GameCanvas.ActualWidth - PlayerShip.ActualWidth));
            Canvas.SetLeft(PlayerShip, left);
        }

        private void Shoot()
        {
            if (bullets.Count < MaxBullets)
            {
                TextBlock bullet = new TextBlock
                {
                    Text = "|",
                    Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White),
                    FontSize = 20
                };
                Canvas.SetLeft(bullet, Canvas.GetLeft(PlayerShip) + PlayerShip.ActualWidth / 2);
                Canvas.SetTop(bullet, Canvas.GetTop(PlayerShip) - bullet.ActualHeight);
                GameCanvas.Children.Add(bullet);
                bullets.Add(bullet);
            }
        }

        private void EndGame()
        {
            gameTimer.Stop();
            if (hits > highScore)
            {
                highScore = hits;
                SaveHighScore();
            }
            StartScreen.Visibility = Visibility.Visible;
            HighScoreTextBlock.Text = $"High Score: {highScore}";
            LastScoreTextBlock.Text = $"Last Score: {hits}";
            GameCanvas.Children.Clear();
            bullets.Clear();
            asteroids.Clear();
        }
    }
}