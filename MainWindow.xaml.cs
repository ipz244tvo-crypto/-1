using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Media.Imaging;

namespace RoguelikeWPF
{
    public partial class MainWindow : Window
    {
        private HashSet<Key> pressedKeys = new HashSet<Key>();
        private Vector playerVelocity;
        private const double PlayerSpeed = 3.0;
        private int playerHP = 20;
        private int maxHP = 20;
        private HashSet<int> roomsWithUpgradeBox = new HashSet<int>();
        private List<AcidBlob> acidBlobs = new List<AcidBlob>();
        private List<Projectile> projectiles = new List<Projectile>();
        private List<AcidPool> acidPools = new List<AcidPool>();
        private bool isReloading = false;
        private List<Medkit> medkits = new List<Medkit>();
        private DispatcherTimer tickTimer;
        private double reloadTime = 1.0;
        private UpgradeBox upgradeBox;
        private DateTime lastShotTime = DateTime.MinValue;
        private int weaponDamageBonus = 0;
        private double playerSpeedBonus = 1.0;
        private bool exitOpened = false;
        private bool isGameOver = false;
        private int playerHitCooldown = 0;
        private const int PlayerHitCooldownMax = 30;
        private DateTime runStartTime;
        private int pistolAmmo = 10;
        private int shotgunAmmo = 5;
        private int autoAmmo = 10;
        private int maxPistolAmmo = 10;
        private int maxShotgunAmmo = 5;
        private int maxAutoAmmo = 10;
        private List<string> currentRunUpgrades = new List<string>();
        private static List<RunRecord> records = new List<RunRecord>();
        private static readonly string RecordsFile = @"C:\work\Курсова\RoguelikeWPF\save\records.json";
        public const int TileSize = 32;
        const int MapWidth = 20;
        const int MapHeight = 13;
        const double BulletSpeed = 8;

        enum WeaponType { Pistol, Shotgun, Auto }
        public enum ZombieType
        {
            Normal,
            Acid,
            Tank,
            AcidTank
        }
        class Medkit
        {
            public UIElement Shape;
            public double X;
            public double Y;
        }

        class Bullet : Projectile
        {
            public override void OnHitWall(MainWindow window)
            {
                window.gameCanvas.Children.Remove(this.Shape);
            }

            public override bool OnHitPlayer(MainWindow window)
            {
                return false;
            }

            public override bool OnHitZombie(MainWindow window, Zombie zombie)
            {
                zombie.HP--;
                if (zombie.HP <= 0)
                {
                    window.ZombieDied(zombie);
                }
                return true;
            }
        }

        public void ZombieDied(Zombie zombie)
        {
            zombies.Remove(zombie);
            gameCanvas.Children.Remove(zombie.Shape);

            double healthPercent = (double)playerHP / maxHP;
            if (healthPercent < 0.6 && random.Next(100) < 20)
            {
                SpawnMedkit(zombie.X, zombie.Y);
            }
        }

        private static void SaveRecords()
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(RecordsFile));
                var json = JsonSerializer.Serialize(records);
                File.WriteAllText(RecordsFile, json);
            }
            catch { }
        }

        private static void LoadRecords()
        {
            try
            {
                if (File.Exists(RecordsFile))
                {
                    var json = File.ReadAllText(RecordsFile);
                    records = JsonSerializer.Deserialize<List<RunRecord>>(json) ?? new List<RunRecord>();
                }
            }
            catch
            {
                records = new List<RunRecord>();
            }
        }
        public void SpawnMedkit(int x, int y)
        {
            Image medkitShape = new Image
            {
                Source = new BitmapImage(new Uri(@"C:\work\Курсова\RoguelikeWPF\Images\Medkit.png", UriKind.Absolute)),
                Width = TileSize - 10,
                Height = TileSize - 10,
                Stretch = Stretch.Fill
            };

            double px = x * TileSize + 5;
            double py = y * TileSize + 5;

            Canvas.SetLeft(medkitShape, px);
            Canvas.SetTop(medkitShape, py);
            gameCanvas.Children.Add(medkitShape);

            medkits.Add(new Medkit
            {
                Shape = medkitShape,
                X = px,
                Y = py
            });
        }

        abstract class Projectile
        {
            public UIElement Shape;
            public Vector Direction;
            public double Speed;

            public abstract void OnHitWall(MainWindow window);
            public abstract bool OnHitPlayer(MainWindow window);
            public abstract bool OnHitZombie(MainWindow window, Zombie zombie);
        }
        class UpgradeBox
        {
            public UIElement Shape;
            public double X;
            public double Y;
            public bool IsActive = true;
        }
        class AcidBlob : Projectile
        {
            public override void OnHitWall(MainWindow window)
            {
                window.gameCanvas.Children.Remove(this.Shape);
            }

            public override bool OnHitPlayer(MainWindow window)
            {
                window.playerHP--;
                window.UpdateHUD();
                if (window.playerHP <= 0)
                {
                    MessageBox.Show("Тебе розплавило кислотою!");
                    window.GameOverToMenu();
                }
                return true;
            }


            public override bool OnHitZombie(MainWindow window, Zombie zombie)
            {
                return false;
            }
        }

        public class Zombie
        {
            public UIElement Shape;
            public int X;
            public int Y;
            public int HP;
            public double PosX;
            public double PosY;
            public ZombieType Type;
            public int AttackCooldown = 0;
            public int MoveCooldown = 0;

            public Point Position => new Point(X, Y);

            public void MoveTowards(Point target, double speed)
            {
                Vector direction = target - new Point(X, Y);
                if (direction.Length > 0)
                {
                    direction.Normalize();
                    X += (int)(direction.X * speed);
                    Y += (int)(direction.Y * speed);
                    Canvas.SetLeft(Shape, X * MainWindow.TileSize + 2);
                    Canvas.SetTop(Shape, Y * MainWindow.TileSize + 2);
                }
            }

        }
        private void GameOverToMenu()
        {
            gameTimer.Stop();

            MainMenu.Visibility = Visibility.Visible;
            gameCanvas.Visibility = Visibility.Hidden;
            weaponText.Visibility = Visibility.Hidden;
            hpText.Visibility = Visibility.Hidden;

            this.KeyDown -= Window_KeyDown;
            this.KeyUp -= Window_KeyUp;
        }

        class AcidPool
        {
            public Rectangle Shape;
            public int Lifetime;
            public double X, Y;
        }

        class AcidZombie : Zombie
        {
            private DispatcherTimer acidShootTimer;

            public AcidZombie()
            {
                acidShootTimer = new DispatcherTimer();
                acidShootTimer.Interval = TimeSpan.FromSeconds(2);
                acidShootTimer.Tick += (s, e) => ShootAcid();
                acidShootTimer.Start();
            }

            private void ShootAcid()
            {
                var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                if (mainWindow == null) return;

                if (!mainWindow.zombies.Contains(this)) return;

                mainWindow.SpawnAcidShot(this);
            }
        }

        class TankZombie : Zombie
        {
            public TankZombie()
            {

            }
        }

        class AcidTankZombie : TankZombie
        {
            public AcidTankZombie()
            {
                //Shape.Fill = Brushes.OrangeRed;
            }

            public void LeaveAcidPuddle(Canvas canvas)
            {
                Rectangle puddle = new Rectangle
                {
                    Width = TileSize,
                    Height = TileSize,
                    Fill = Brushes.YellowGreen,
                    Opacity = 0.5
                };
                Canvas.SetLeft(puddle, X * TileSize);
                Canvas.SetTop(puddle, Y * TileSize);
                canvas.Children.Add(puddle);
            }
        }
        public class RunRecord
        {
            public TimeSpan Time { get; set; }
            public List<string> Upgrades { get; set; }
        }

        public class Room
        {
            public int[,] Layout;
            public Room(int[,] layout) => Layout = layout;
        }

        private List<Room> rooms;
        private int currentRoomIndex = 0;
        private int playerX = 1, playerY = 1;
        private FrameworkElement player;
        private DispatcherTimer gameTimer;
        private List<Bullet> bullets;
        private WeaponType currentWeapon = WeaponType.Pistol;

        private void ShowRecords_Click(object sender, RoutedEventArgs e)
        {
            ShowRecordsTable();
        }
        private void DrawRoom(Room room)
        {
            for (int y = 0; y < room.Layout.GetLength(0); y++)
            {
                for (int x = 0; x < room.Layout.GetLength(1); x++)
                {
                    int tile = room.Layout[y, x];

                    if (tile == 2 && !exitOpened)
                        tile = 1;

                    if (tile == 0)
                    {
                        var img = new Image
                        {
                            Width = TileSize,
                            Height = TileSize,
                            Source = new BitmapImage(new Uri(@"C:\work\Курсова\RoguelikeWPF\Images\floor.png", UriKind.Absolute)),
                            Stretch = Stretch.Fill
                        };
                        Canvas.SetLeft(img, x * TileSize);
                        Canvas.SetTop(img, y * TileSize);
                        gameCanvas.Children.Add(img);
                    }
                    else if (tile == 1)
                    {
                        var img = new Image
                        {
                            Width = TileSize,
                            Height = TileSize,
                            Source = new BitmapImage(new Uri(@"C:\work\Курсова\RoguelikeWPF\Images\wall.png", UriKind.Absolute)),
                            Stretch = Stretch.Fill
                        };
                        Canvas.SetLeft(img, x * TileSize);
                        Canvas.SetTop(img, y * TileSize);
                        gameCanvas.Children.Add(img);
                    }
                    else if (tile == 2)
                    {
                        var img = new Image
                        {
                            Width = TileSize,
                            Height = TileSize,
                            Source = new BitmapImage(new Uri(@"C:\work\Курсова\RoguelikeWPF\Images\darkness.png", UriKind.Absolute)),
                            Stretch = Stretch.Fill
                        };
                        Canvas.SetLeft(img, x * TileSize);
                        Canvas.SetTop(img, y * TileSize);
                        gameCanvas.Children.Add(img);
                    }
                    else
                    {
                        Rectangle rect = new Rectangle
                        {
                            Width = TileSize,
                            Height = TileSize,
                            Fill = Brushes.LightGray
                        };
                        Canvas.SetLeft(rect, x * TileSize);
                        Canvas.SetTop(rect, y * TileSize);
                        gameCanvas.Children.Add(rect);
                    }
                }
            }
        }

        private void CenterPlayerInRoom()
        {
            var fe = player as FrameworkElement;
            if (fe != null)
            {
                double centerX = (rooms[currentRoomIndex].Layout.GetLength(1) * TileSize) / 2 - fe.Width / 2;
                double centerY = (rooms[currentRoomIndex].Layout.GetLength(0) * TileSize) / 2 - fe.Height / 2;
                Canvas.SetLeft(player, centerX);
                Canvas.SetTop(player, centerY);
            }
        }


        private void WindowMode_Checked(object sender, RoutedEventArgs e)
        {
            if (windowedRadio.IsChecked == true)
            {
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.WindowState = WindowState.Normal;
            }
            else if (borderlessRadio.IsChecked == true)
            {
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Normal;
            }
            else if (fullscreenRadio.IsChecked == true)
            {
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            LoadRecords();

        }

        private void TopmostCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // this.Topmost = topmostCheckBox.IsChecked == true;
        }
        private bool HasLineOfSight(Point from, Point to)
        {
            var layout = rooms[currentRoomIndex].Layout;
            int steps = 20;
            double dx = (to.X - from.X) / steps;
            double dy = (to.Y - from.Y) / steps;

            for (int i = 1; i <= steps; i++)
            {
                double checkX = from.X + dx * i;
                double checkY = from.Y + dy * i;

                int tileX = (int)(checkX / TileSize);
                int tileY = (int)(checkY / TileSize);

                int rows = layout.GetLength(0);
                int cols = layout.GetLength(1);
                if (tileX < 0 || tileY < 0 || tileX >= cols || tileY >= rows)
                    return false;

                if (layout[tileY, tileX] == 1)
                    return false;
            }

            return true;
        }

        private async void AnimateZombieAttack(Zombie zombie)
        {
            Brush originalBrush = null;
            if (zombie.Shape is Rectangle rect)
            {
                originalBrush = rect.Fill;
                rect.Fill = Brushes.White;
            }

            await Task.Delay(120);

            if (zombie.Shape is Rectangle rect2)
            {
                switch (zombie.Type)
                {
                    case ZombieType.Acid:
                        rect2.Fill = Brushes.LimeGreen;
                        break;
                    case ZombieType.Tank:
                        rect2.Fill = Brushes.DarkRed;
                        break;
                    case ZombieType.AcidTank:
                        rect2.Fill = Brushes.OrangeRed;
                        break;
                    default:
                        rect2.Fill = Brushes.Red;
                        break;
                }
            }
        }
        private void AddUpgradeBox()
        {
            int boxGridX = 10;
            int boxGridY = 6;
            if (rooms[0].Layout[boxGridY, boxGridX] != 0)
            {
                bool found = false;
                for (int y = 1; y < MapHeight - 1 && !found; y++)
                {
                    for (int x = 1; x < MapWidth - 1 && !found; x++)
                    {
                        if (rooms[0].Layout[y, x] == 0)
                        {
                            boxGridX = x;
                            boxGridY = y;
                            found = true;
                        }
                    }
                }
            }
            double boxX = boxGridX * TileSize;
            double boxY = boxGridY * TileSize;

            Image boxShape = new Image
            {
                Source = new BitmapImage(new Uri(@"C:\work\Курсова\RoguelikeWPF\Images\box.png", UriKind.Absolute)),
                Width = TileSize,
                Height = TileSize,
                Stretch = Stretch.Fill
            };
            Canvas.SetLeft(boxShape, boxX);
            Canvas.SetTop(boxShape, boxY);
            gameCanvas.Children.Add(boxShape);
            Panel.SetZIndex(boxShape, 1000);

            upgradeBox = new UpgradeBox
            {
                Shape = boxShape,
                X = boxX,
                Y = boxY,
                IsActive = true
            };
        }
        private void InitGame(bool fullRestart = true)
        {
            
            if (player != null && gameCanvas.Children.Contains(player))
                gameCanvas.Children.Remove(player);

            if (fullRestart)
            {
                currentRoomIndex = 0;
                playerX = 1;
                playerY = 1;
                playerHP = maxHP;
                playerSpeedBonus = 1.0;
            }

            if (rooms == null)
            {
                CreateRooms();
            }
            UpdateSpeedText();
            var layout = rooms[currentRoomIndex].Layout;
            if (playerY < 0 || playerY >= layout.GetLength(0) ||
                playerX < 0 || playerX >= layout.GetLength(1) ||
                layout[playerY, playerX] == 1)
            {
                bool found = false;
                for (int y = 1; y < layout.GetLength(0) - 1 && !found; y++)
                {
                    for (int x = 1; x < layout.GetLength(1) - 1 && !found; x++)
                    {
                        if (layout[y, x] == 0)
                        {
                            playerX = x;
                            playerY = y;
                            found = true;
                        }
                    }
                }
                if (!found)
                {
                    playerX = 1;
                    playerY = 1;
                }
            }

            if (fullRestart)
            {
                runStartTime = DateTime.Now;
                currentRunUpgrades.Clear();
            }
            pressedKeys.Clear();
            if (gameTimer != null)
            {
                gameTimer.Stop();
                gameTimer.Tick -= GameLoop;
                gameTimer.Tick -= GameTick;
            }
            if (tickTimer != null)
            {
                tickTimer.Stop();
                tickTimer.Tick -= GameTick;
            }

            isGameOver = false;
            exitOpened = false;

            if (fullRestart)
                gameCanvas.Children.Clear();

            bullets = new List<Bullet>();
            zombies = new List<Zombie>();

            DrawRoom(rooms[currentRoomIndex]);
            gameCanvas.Width = MapWidth * TileSize;
            gameCanvas.Height = MapHeight * TileSize;

            if (currentRoomIndex == 0 && !roomsWithUpgradeBox.Contains(currentRoomIndex))
            {
                AddUpgradeBox();
                roomsWithUpgradeBox.Add(currentRoomIndex);
            }

            player = new Image
            {
                Width = TileSize - 4,
                Height = TileSize - 4,
                Source = new BitmapImage(new Uri(@"C:\work\Курсова\RoguelikeWPF\Images\player.png", UriKind.Absolute)),
                Stretch = Stretch.Fill
            };
            Canvas.SetLeft(player, playerX * TileSize + 2);
            Canvas.SetTop(player, playerY * TileSize + 2);
            gameCanvas.Children.Add(player);
            Panel.SetZIndex(player, 1000);

            UpdatePlayerPosition();

            if (currentRoomIndex != 0)
                SpawnZombies();

            UpdateHUD();

            currentWeapon = WeaponType.Pistol;
            if (weaponText != null)
                weaponText.Text = "Зброя: Пістолет";

            if (hpText != null)
                hpText.Text = $"Здоров’я: {playerHP} / {maxHP}";

            gameTimer = new DispatcherTimer();
            gameTimer.Interval = TimeSpan.FromMilliseconds(16);
            gameTimer.Tick += GameLoop;
            gameTimer.Start();

            tickTimer = new DispatcherTimer();
            tickTimer.Interval = TimeSpan.FromMilliseconds(20);
            tickTimer.Tick += GameTick;
            tickTimer.Start();

            this.KeyDown -= Window_KeyDown;
            this.KeyUp -= Window_KeyUp;
            this.KeyDown += Window_KeyDown;
            this.KeyUp += Window_KeyUp;
        }

        private void CreateRooms()
        {
            var rnd = new Random();

            Room startRoom = new Room(new int[,] {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    });
            Room bossRoom = new Room(new int[,] {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    });

            var allTemplates = new List<Room>
{
    new Room(new int[,] {
         {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
         {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
         {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,1},
         {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,1},
         {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
         {1,0,0,1,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,1},
         {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
         {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
         {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
         {1,0,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,1,1},
         {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
         {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
         {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    }),

    new Room(new int[,] {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,1,0,0,0,1,0,0,0,1},
        {1,0,1,0,0,0,0,0,0,0,0,1,0,0,0,1,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,1,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,0,0,1,1,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,0,1,1,1,1,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    }),
    new Room(new int[,] {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,1,1,0,0,0,0,0,0,0,1,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,1},
        {1,1,1,0,0,0,0,0,0,0,0,1,1,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    }),
    new Room(new int[,] {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    }),
    new Room(new int[,] {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    }),
    new Room(new int[,] {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,1,0,0,0,0,0,1,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    }),

    new Room(new int[,] {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    }),
    new Room(new int[,] {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    }),
    new Room(new int[,] {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    }),
    new Room(new int[,] {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    }),
    new Room(new int[,] {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    }),
    new Room(new int[,] {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    }),
    new Room(new int[,] {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    }),
    new Room(new int[,] {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    }),
    new Room(new int[,] {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,1,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,2},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    })
};


            int roomsToAdd = 10;
            List<Room> randomRooms;
            if (allTemplates.Count >= roomsToAdd)
            {
                randomRooms = allTemplates.OrderBy(x => rnd.Next()).Take(roomsToAdd).ToList();
            }
            else
            {
                randomRooms = new List<Room>();
                for (int i = 0; i < roomsToAdd; i++)
                    randomRooms.Add(allTemplates[rnd.Next(allTemplates.Count)]);
            }

            rooms = new List<Room>();
            rooms.Add(startRoom);
            rooms.AddRange(randomRooms);
            rooms.Add(bossRoom);
        }

        private void GameLoop(object sender, EventArgs e)
        {
            UpdatePlayerVelocity();
            UpdateBullets();
            UpdateZombies();
            UpdatePlayerVelocity();

            double newX = Canvas.GetLeft(player) + playerVelocity.X;
            double newY = Canvas.GetTop(player) + playerVelocity.Y;

            if (!IsCollidingWithWall(newX, newY))
            {
                Canvas.SetLeft(player, newX);
                Canvas.SetTop(player, newY);

                playerX = (int)(newX / TileSize);
                playerY = (int)(newY / TileSize);
            }
            if (rooms[currentRoomIndex].Layout[playerY, playerX] == 2)
            {
                NextRoom();
                return;
            }

        }
        private void UpdateAmmoText()
        {
            switch (currentWeapon)
            {
                case WeaponType.Pistol:
                    ammoText.Text = $"Патрони: {pistolAmmo} / {maxPistolAmmo}";
                    break;
                case WeaponType.Shotgun:
                    ammoText.Text = $"Патрони: {shotgunAmmo} / {maxShotgunAmmo}";
                    break;
                case WeaponType.Auto:
                    ammoText.Text = $"Патрони: {autoAmmo} / {maxAutoAmmo}";
                    break;
            }
        }
        private async void Reload()
        {
            if (isReloading) return;
            isReloading = true;
            ammoText.Text = "Перезарядка...";
            await Task.Delay(1000);
            switch (currentWeapon)
            {
                case WeaponType.Pistol:
                    pistolAmmo = maxPistolAmmo;
                    break;
                case WeaponType.Shotgun:
                    shotgunAmmo = maxShotgunAmmo;
                    break;
                case WeaponType.Auto:
                    autoAmmo = maxAutoAmmo;
                    break;
            }
            isReloading = false;
            UpdateAmmoText();
        }
        private void MoveToNextRoom()
        {
            currentRoomIndex++;
            if (currentRoomIndex < rooms.Count)
            {
                ClearRoomVisuals();
                DrawRoom(rooms[currentRoomIndex]);

                int enemiesToSpawn = currentRoomIndex + 3;
                SpawnZombies();

                CenterPlayerInRoom();
            }
        }
        private void UpdateDamageText()
        {
            int baseDamage = 1;
            switch (currentWeapon)
            {
                case WeaponType.Pistol:
                    baseDamage = 1;
                    break;
                case WeaponType.Shotgun:
                    baseDamage = 3;
                    break;
                case WeaponType.Auto:
                    baseDamage = 1;
                    break;
            }
            damageText.Text = $"Урон: {baseDamage + weaponDamageBonus}";
        }
        private void PistolButton_Click(object sender, RoutedEventArgs e)
        {
            currentWeapon = WeaponType.Pistol;
            weaponText.Text = "Зброя: Пістолет";
            UpdateAmmoText();
            UpdateDamageText();
        }

        private void ShotgunButton_Click(object sender, RoutedEventArgs e)
        {
            currentWeapon = WeaponType.Shotgun;
            weaponText.Text = "Зброя: Дробовик";
            UpdateAmmoText();
            UpdateDamageText();
        }

        private void AutoButton_Click(object sender, RoutedEventArgs e)
        {
            currentWeapon = WeaponType.Auto;
            weaponText.Text = "Зброя: Автомат";
            UpdateAmmoText();
            UpdateDamageText();
        }
        private void HideWeaponSelectPanel_Click(object sender, RoutedEventArgs e)
        {
            WeaponSelectPanel.Visibility = Visibility.Collapsed;
        }
        private void UpdatePlayerVelocity()
        {
            playerVelocity = new Vector();

            if (pressedKeys.Contains(Key.W)) playerVelocity.Y -= 1;
            if (pressedKeys.Contains(Key.S)) playerVelocity.Y += 1;
            if (pressedKeys.Contains(Key.A)) playerVelocity.X -= 1;
            if (pressedKeys.Contains(Key.D)) playerVelocity.X += 1;

            if (playerVelocity.Length > 0)
            {
                playerVelocity.Normalize();
                playerVelocity *= PlayerSpeed * playerSpeedBonus;
            }
        }

        private bool IsLineOfSightClear(Point from, Point to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            double xStep = dx / (double)steps;
            double yStep = dy / (double)steps;

            for (int i = 0; i <= steps; i++)
            {
                int x = (int)Math.Round(from.X + i * xStep);
                int y = (int)Math.Round(from.Y + i * yStep);
                if (rooms[currentRoomIndex].Layout[y, x] == 1)
                    return false;
            }
            return true;
        }

        private bool IsCollidingWithWall(double x, double y)
        {
            var room = rooms[currentRoomIndex];

            int tileXStart = (int)(x / TileSize);
            int tileYStart = (int)(y / TileSize);

            int tileXEnd = (int)((x + TileSize - 1) / TileSize);
            int tileYEnd = (int)((y + TileSize - 1) / TileSize);

            int rows = room.Layout.GetLength(0);
            int cols = room.Layout.GetLength(1);
            if (tileXStart < 0 || tileYStart < 0 || tileXEnd >= cols || tileYEnd >= rows)
                return true;

            for (int ty = tileYStart; ty <= tileYEnd; ty++)
            {
                for (int tx = tileXStart; tx <= tileXEnd; tx++)
                {
                    if (room.Layout[ty, tx] == 1)
                        return true;
                }
            }

            return false;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (isUpgradeDialogOpen) return;

            if (e.Key == Key.D1)
            {
                PistolButton_Click(null, null);
                return;
            }
            if (e.Key == Key.D2)
            {
                ShotgunButton_Click(null, null);
                return;
            }
            if (e.Key == Key.D3)
            {
                AutoButton_Click(null, null);
                return;
            }

            if (e.Key == Key.R)
            {
                Reload();
            }
            if (e.Key == Key.Space)
            {
                Shoot();
            }
            pressedKeys.Add(e.Key);
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (isUpgradeDialogOpen) return;
            pressedKeys.Remove(e.Key);
        }


        private void StartGame_Click(object sender, RoutedEventArgs e)
        {
            MainMenu.Visibility = Visibility.Collapsed;
            gameCanvas.Visibility = Visibility.Visible;
            weaponText.Visibility = Visibility.Visible;
            hpText.Visibility = Visibility.Visible;

            InitGame();
        }

        private void SpawnZombies()
        {
            zombies = new List<Zombie>();
            int[,] layout = rooms[currentRoomIndex].Layout;
            int rows = layout.GetLength(0);
            int cols = layout.GetLength(1);

            for (int i = 0; i < 5; i++)
            {
                int zx, zy;
                int attempts = 0;

                do
                {
                    zx = random.Next(1, cols - 1);
                    zy = random.Next(1, rows - 1);
                    attempts++;
                }
                while ((layout[zy, zx] != 0 || (zx == playerX && zy == playerY)) && attempts < 100);

                if (attempts >= 100) continue;

                ZombieType type;
                int roll = random.Next(1, 11);

                if (roll <= 2)
                    type = ZombieType.Tank;
                else if (roll <= 5)
                    type = ZombieType.Acid;
                else
                    type = ZombieType.Normal;

                Brush color = Brushes.Red;
                int hp = 3;

                switch (type)
                {
                    case ZombieType.Acid:
                        color = Brushes.LimeGreen;
                        break;
                    case ZombieType.Tank:
                        color = Brushes.DarkRed;
                        hp = 6;
                        break;
                    case ZombieType.AcidTank:
                        color = Brushes.OrangeRed;
                        hp = 6;
                        break;
                }

                UIElement zShape;
                if (type == ZombieType.Normal)
                {
                    var img = new Image
                    {
                        Width = TileSize - 4,
                        Height = TileSize - 4,
                        Source = new BitmapImage(new Uri(@"C:\work\Курсова\RoguelikeWPF\Images\Zombie5x.gif", UriKind.Absolute))
                    };
                    Canvas.SetLeft(img, zx * TileSize + 2);
                    Canvas.SetTop(img, zy * TileSize + 2);
                    gameCanvas.Children.Add(img);
                    zShape = img;
                }
                else if (type == ZombieType.Acid)
                {
                    var img = new Image
                    {
                        Width = TileSize - 4,
                        Height = TileSize - 4,
                        Source = new BitmapImage(new Uri(@"C:\work\Курсова\RoguelikeWPF\Images\zombie-pixel.png", UriKind.Absolute))
                    };
                    Canvas.SetLeft(img, zx * TileSize + 2);
                    Canvas.SetTop(img, zy * TileSize + 2);
                    gameCanvas.Children.Add(img);
                    zShape = img;
                }
                else if (type == ZombieType.Tank)
                {
                    var img = new Image
                    {
                        Width = TileSize - 4,
                        Height = TileSize - 4,
                        Source = new BitmapImage(new Uri(@"C:\work\Курсова\RoguelikeWPF\Images\zombi.tank.png", UriKind.Absolute))
                    };
                    Canvas.SetLeft(img, zx * TileSize + 2);
                    Canvas.SetTop(img, zy * TileSize + 2);
                    gameCanvas.Children.Add(img);
                    zShape = img;
                }
                else
                {
                    var rect = new Rectangle
                    {
                        Width = TileSize - 4,
                        Height = TileSize - 4,
                        Fill = color
                    };
                    Canvas.SetLeft(rect, zx * TileSize + 2);
                    Canvas.SetTop(rect, zy * TileSize + 2);
                    gameCanvas.Children.Add(rect);
                    zShape = rect;
                }

                Zombie zombie;
                switch (type)
                {
                    case ZombieType.Acid:
                        zombie = new AcidZombie();
                        break;
                    case ZombieType.Tank:
                        zombie = new TankZombie();
                        break;
                    case ZombieType.AcidTank:
                        zombie = new AcidTankZombie();
                        break;
                    default:
                        zombie = new Zombie();
                        break;
                }
                zombie.Shape = zShape;
                zombie.X = zx;
                zombie.Y = zy;
                zombie.HP = hp;
                zombie.Type = type;
                zombie.AttackCooldown = 0;
                zombie.PosX = zx * MainWindow.TileSize + 2;
                zombie.PosY = zy * MainWindow.TileSize + 2;
                Canvas.SetLeft(zombie.Shape, zombie.PosX);
                Canvas.SetTop(zombie.Shape, zombie.PosY);

                zombies.Add(zombie);
            }
        }

        private List<Zombie> zombies = new List<Zombie>();
        private Random random = new Random();



        private void ExitGame_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void SelectWeapon_Click(object sender, RoutedEventArgs e)
        {
            WeaponSelectPanel.Visibility = Visibility.Visible;
        }

        private int zombieMoveCounter = 0;

        private void UpdateZombies()
        {
            double zombieSpeed = 1.0;

            foreach (var z in zombies.ToList())
            {
                if ((z.Type == ZombieType.Acid || z.Type == ZombieType.AcidTank) &&
                    HasLineOfSight(new Point(z.X * TileSize, z.Y * TileSize), new Point(playerX * TileSize, playerY * TileSize)))
                {
                    z.AttackCooldown++;
                    if (z.AttackCooldown >= 50)
                    {
                        z.AttackCooldown = 0;
                        SpawnAcid(z.X, z.Y, playerX, playerY);
                    }

                    if (z.Type == ZombieType.AcidTank)
                    {
                        SpawnAcidPool(z.X, z.Y);
                    }
                    continue;
                }

                double targetX = Canvas.GetLeft(player);
                double targetY = Canvas.GetTop(player);

                double dx = targetX - z.PosX;
                double dy = targetY - z.PosY;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist > 1)
                {
                    double moveX = zombieSpeed * dx / dist;
                    double moveY = zombieSpeed * dy / dist;

                    double nextPosX = z.PosX + moveX;
                    double nextPosY = z.PosY + moveY;

                    int tileX = (int)((nextPosX + TileSize / 2) / TileSize);
                    int tileY = (int)((nextPosY + TileSize / 2) / TileSize);
                    int[,] layout = rooms[currentRoomIndex].Layout;

                    if (tileX >= 0 && tileY >= 0 &&
                        tileX < layout.GetLength(1) && tileY < layout.GetLength(0) &&
                        layout[tileY, tileX] != 1)
                    {
                        z.PosX = nextPosX;
                        z.PosY = nextPosY;

                        Canvas.SetLeft(z.Shape, z.PosX);
                        Canvas.SetTop(z.Shape, z.PosY);

                        z.X = tileX;
                        z.Y = tileY;
                    }
                }

                Rect zRect = new Rect(z.PosX, z.PosY, TileSize - 4, TileSize - 4);
                Rect pRect = new Rect(Canvas.GetLeft(player), Canvas.GetTop(player), player.Width, player.Height);

            }
        }


        private void SpawnAcid(int zx, int zy, int px, int py)
        {
            double startX = zx * TileSize + TileSize / 2;
            double startY = zy * TileSize + TileSize / 2;
            double endX = px * TileSize + TileSize / 2;
            double endY = py * TileSize + TileSize / 2;

            Vector direction = new Point(endX, endY) - new Point(startX, startY);

            if (direction.Length == 0)
                direction = new Vector(1, 0);

            direction.Normalize();

            Ellipse acidBlob = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.GreenYellow
            };

            Canvas.SetLeft(acidBlob, startX - 5);
            Canvas.SetTop(acidBlob, startY - 5);
            gameCanvas.Children.Add(acidBlob);

            acidBlobs.Add(new AcidBlob
            {
                Shape = acidBlob,
                Direction = direction,
                Speed = 3
            });
        }


        private void SpawnAcidPool(int x, int y)
        {
            Rectangle puddle = new Rectangle
            {
                Width = TileSize,
                Height = TileSize,
                Fill = Brushes.YellowGreen,
                Opacity = 0.5
            };

            Canvas.SetLeft(puddle, x * TileSize);
            Canvas.SetTop(puddle, y * TileSize);
            gameCanvas.Children.Add(puddle);
        }

        private bool ZombieCanSeePlayer(int zx, int zy, int px, int py)
        {
            var layout = rooms[currentRoomIndex].Layout;

            if (zx == px)
            {
                int minY = Math.Min(zy, py);
                int maxY = Math.Max(zy, py);
                for (int y = minY + 1; y < maxY; y++)
                {
                    if (layout[y, zx] == 1) return false;
                }
                return true;
            }
            else if (zy == py)
            {
                int minX = Math.Min(zx, px);
                int maxX = Math.Max(zx, px);
                for (int x = minX + 1; x < maxX; x++)
                {
                    if (layout[zy, x] == 1) return false;
                }
                return true;
            }

            return false;
        }


        private void UpdateHUD()
        {
            if (hpText != null)
                hpText.Text = $"Здоров’я: {playerHP} / {maxHP}";
        }


        private void UpdatePlayerPosition()
        {
            Canvas.SetLeft(player, playerX * TileSize + 2);
            Canvas.SetTop(player, playerY * TileSize + 2);
        }

        private bool IsWalkable(int tile)
        {
            return tile == 0 || tile == 2;
        }

        private void MovePlayer(int dx, int dy)
        {
            int newX = playerX + dx;
            int newY = playerY + dy;
            var layout = rooms[currentRoomIndex].Layout;

            if (newX >= 0 && newX < layout.GetLength(1) &&
                newY >= 0 && newY < layout.GetLength(0) &&
                layout[newY, newX] != 1)
            {
                playerX = newX;
                playerY = newY;
                UpdatePlayerPosition();

                if (layout[playerY, playerX] == 2)
                {
                    NextRoom();
                }
            }
        }
        private void ShowRecordsTable()
        {
            if (records.Count == 0)
            {
                MessageBox.Show("Ще немає рекордів!");
                return;
            }

            string table = "Топ-5 проходжень:\n\n";
            int i = 1;
            foreach (var rec in records)
            {
                table += $"{i++}. Час: {rec.Time:mm\\:ss} | Прокачки: {string.Join(", ", rec.Upgrades)}\n";
            }

            MessageBox.Show(table, "Таблиця рекордів", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void NextRoom()
        {
            pressedKeys.Clear();

            if (currentRoomIndex >= rooms.Count - 1)
            {
                var runTime = DateTime.Now - runStartTime;
                records.Add(new RunRecord
                {
                    Time = runTime,
                    Upgrades = new List<string>(currentRunUpgrades)
                });
                records = records.OrderBy(r => r.Time).Take(5).ToList();
                SaveRecords();
                ShowRecordsTable();

                MessageBox.Show("Дякую за те що грали!", "Кінець гри", MessageBoxButton.OK, MessageBoxImage.Information);
                GameOverToMenu();
                return;
            }

            int nextRoomIndex = (currentRoomIndex + 1) % rooms.Count;
            int rows = rooms[nextRoomIndex].Layout.GetLength(0);
            int cols = rooms[nextRoomIndex].Layout.GetLength(1);
            Rectangle fade = new Rectangle
            {
                Width = cols * TileSize,
                Height = rows * TileSize,
                Fill = Brushes.Black,
                Opacity = 0
            };

            Panel.SetZIndex(fade, 9999);
            gameCanvas.Children.Add(fade);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500));
            fade.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            await Task.Delay(500);

            ClearRoomVisuals();

            currentRoomIndex = nextRoomIndex;

            var layout = rooms[currentRoomIndex].Layout;
            bool found = false;
            for (int y = 1; y < layout.GetLength(0) - 1 && !found; y++)
            {
                for (int x = 1; x < layout.GetLength(1) - 1 && !found; x++)
                {
                    if (layout[y, x] == 0)
                    {
                        playerX = x;
                        playerY = y;
                        found = true;
                    }
                }
            }
            if (!found)
            {
                playerX = 1;
                playerY = 1;
            }

            InitGame(false);

            foreach (var z in zombies)
                z.MoveCooldown = 50;

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
            fadeOut.Completed += (s, e) => gameCanvas.Children.Remove(fade);
            fade.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }


        private void Shoot()
        {
            if (isReloading) return;

            TimeSpan timeSinceLastShot = DateTime.Now - lastShotTime;
            double cooldown;
            int ammo = 0;
            switch (currentWeapon)
            {
                case WeaponType.Pistol:
                    cooldown = 0.4;
                    ammo = pistolAmmo;
                    break;
                case WeaponType.Shotgun:
                    cooldown = 1.0;
                    ammo = shotgunAmmo;
                    break;
                case WeaponType.Auto:
                    cooldown = 0.1;
                    ammo = autoAmmo;
                    break;
                default:
                    cooldown = 0.5;
                    break;
            }

            if (ammo <= 0) return;
            if (timeSinceLastShot.TotalSeconds < cooldown) return;

            lastShotTime = DateTime.Now;

            double px = Canvas.GetLeft(player) + player.Width / 2;
            double py = Canvas.GetTop(player) + player.Height / 2;

            Point mousePos = Mouse.GetPosition(gameCanvas);
            Vector direction = mousePos - new Point(px, py);
            if (direction.Length == 0) direction = new Vector(1, 0);
            direction.Normalize();

            switch (currentWeapon)
            {
                case WeaponType.Pistol:
                    pistolAmmo--;
                    CreateBullet(px, py, direction, BulletSpeed);
                    break;
                case WeaponType.Shotgun:
                    shotgunAmmo--;
                    CreateBullet(px, py, direction + new Vector(0, -0.2), BulletSpeed * 0.9);
                    CreateBullet(px, py, direction, BulletSpeed * 0.9);
                    CreateBullet(px, py, direction + new Vector(0, 0.2), BulletSpeed * 0.9);
                    break;
                case WeaponType.Auto:
                    autoAmmo--;
                    CreateBullet(px, py, direction, BulletSpeed, Brushes.Orange, 1.0);
                    break;
            }
            UpdateAmmoText();
        }
        private void ClearExitTilesInLayout()
        {
            var layout = rooms[currentRoomIndex].Layout;
            for (int y = 0; y < layout.GetLength(0); y++)
                for (int x = 0; x < layout.GetLength(1); x++)
                    if (layout[y, x] == 2)
                        layout[y, x] = 0;
        }
        private void CreateBullet(double x, double y, Vector direction, double speed, Brush color = null, double thickness = 1.0)
        {
            direction.Normalize();
            Image bulletShape = new Image
            {
                Width = 12 * thickness,
                Height = 12 * thickness,
                Source = new BitmapImage(new Uri(@"C:\work\Курсова\RoguelikeWPF\Images\projectile.png", UriKind.Absolute)),
                Stretch = Stretch.Fill
            };
            Canvas.SetLeft(bulletShape, x);
            Canvas.SetTop(bulletShape, y);
            gameCanvas.Children.Add(bulletShape);

            bullets.Add(new Bullet
            {
                Shape = bulletShape,
                Direction = direction,
                Speed = speed
            });
        }

        private void UpdateSpeedText()
        {
            int percent = (int)(playerSpeedBonus * 100);
            if (FindName("speedText") is TextBlock speedText)
                speedText.Text = $"Швидкість: {percent}%";
        }
        private bool isUpgradeDialogOpen = false;
        private void ShowUpgradeDialog()
        {
            isUpgradeDialogOpen = true;
            pressedKeys.Clear();
            var result = MessageBox.Show(
                "Вибери покращення:\n\n" +
                "Так — +5 до максимального здоров'я\n" +
                "Ні — +1 до урону зброї\n" +
                "Скасувати — +20% до швидкості руху",
                "Прокачка",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                maxHP += 5;
                playerHP += 5;
                UpdateHUD();
                currentRunUpgrades.Add("+5 HP");
            }
            else if (result == MessageBoxResult.No)
            {
                weaponDamageBonus += 1;
                UpdateDamageText();
                currentRunUpgrades.Add("+1 Damage");
            }
            else if (result == MessageBoxResult.Cancel)
            {
                playerSpeedBonus += 0.2;
                currentRunUpgrades.Add("+20% Speed");
                UpdateSpeedText();
            }
            pressedKeys.Clear();
            playerVelocity = new Vector(0, 0);
            UpdatePlayerVelocity();
            isUpgradeDialogOpen = false;
        }
        private void GameTick(object sender, EventArgs e)
        {
            UpdateZombies();
            UpdateAcidBlobs();
            UpdateAcidPools();

            if (playerHitCooldown > 0)
                playerHitCooldown--;

            foreach (var zombie in zombies)
            {
                double zWidth = 0, zHeight = 0;
                if (zombie.Shape is FrameworkElement fe)
                {
                    zWidth = fe.Width;
                    zHeight = fe.Height;
                }

                Rect zRect = new Rect(Canvas.GetLeft(zombie.Shape), Canvas.GetTop(zombie.Shape), zWidth, zHeight);
                Rect pRect = new Rect(Canvas.GetLeft(player), Canvas.GetTop(player), player.Width, player.Height);

                if (zRect.IntersectsWith(pRect) && playerHitCooldown == 0)
                {
                    playerHP--;
                    UpdateHUD();
                    AnimateZombieAttack(zombie);
                    playerHitCooldown = PlayerHitCooldownMax;

                    if (playerHP <= 0 && !isGameOver)
                    {
                        isGameOver = true;
                        gameTimer.Stop();
                        MessageBox.Show("Гравець загинув!");
                        GameOverToMenu();
                        return;
                    }
                    break;
                }
            }

            if (upgradeBox != null && upgradeBox.IsActive)
            {
                Rect boxRect = new Rect(upgradeBox.X, upgradeBox.Y, TileSize - 16, TileSize - 16);
                Rect pRect = new Rect(Canvas.GetLeft(player), Canvas.GetTop(player), player.Width, player.Height);

                if (boxRect.IntersectsWith(pRect))
                {
                    upgradeBox.IsActive = false;
                    gameCanvas.Children.Remove(upgradeBox.Shape);

                    ShowUpgradeDialog();
                    pressedKeys.Clear();
                }
            }

            for (int i = medkits.Count - 1; i >= 0; i--)
            {
                Rect mRect = new Rect(medkits[i].X, medkits[i].Y, TileSize - 10, TileSize - 10);
                Rect pRect = new Rect(Canvas.GetLeft(player), Canvas.GetTop(player), player.Width, player.Height);

                if (mRect.IntersectsWith(pRect))
                {
                    if (playerHP < maxHP)
                    {
                        playerHP++;
                        UpdateHUD();
                    }

                    gameCanvas.Children.Remove(medkits[i].Shape);
                    medkits.RemoveAt(i);
                }
            }

            UpdateBullets();
        }

        private void UpdateBullets()
        {
            List<Bullet> toRemove = new List<Bullet>();
            List<Zombie> deadZombies = new List<Zombie>();

            foreach (var bullet in bullets)
            {
                double x = Canvas.GetLeft(bullet.Shape);
                double y = Canvas.GetTop(bullet.Shape);
                x += bullet.Direction.X * bullet.Speed;
                y += bullet.Direction.Y * bullet.Speed;
                Canvas.SetLeft(bullet.Shape, x);
                Canvas.SetTop(bullet.Shape, y);

                int rows = rooms[currentRoomIndex].Layout.GetLength(0);
                int cols = rooms[currentRoomIndex].Layout.GetLength(1);
                if (x < 0 || x > cols * TileSize || y < 0 || y > rows * TileSize)
                {
                    toRemove.Add(bullet);
                    gameCanvas.Children.Remove(bullet.Shape);
                    continue;
                }

                int tileX = (int)(x / TileSize);
                int tileY = (int)(y / TileSize);

                if (tileX >= 0 && tileY >= 0 &&
                  tileX < cols && tileY < rows &&
                  rooms[currentRoomIndex].Layout[tileY, tileX] == 1)
                {
                    // CreateAcidPool(x, y);

                    toRemove.Add(bullet);
                    gameCanvas.Children.Remove(bullet.Shape);
                    continue;
                }

                foreach (var zombie in zombies.ToList())
                {
                    double bWidth = 0, bHeight = 0;
                    if (bullet.Shape is FrameworkElement bfe)
                    {
                        bWidth = bfe.Width;
                        bHeight = bfe.Height;
                    }
                    Rect bRect = new Rect(Canvas.GetLeft(bullet.Shape), Canvas.GetTop(bullet.Shape), bWidth, bHeight);
                    double zWidth = 0, zHeight = 0;
                    if (zombie.Shape is FrameworkElement zfe)
                    {
                        zWidth = zfe.Width;
                        zHeight = zfe.Height;
                    }
                    Rect zRect = new Rect(Canvas.GetLeft(zombie.Shape), Canvas.GetTop(zombie.Shape), zWidth, zHeight);

                    if (bRect.IntersectsWith(zRect))
                    {
                        zombie.HP -= 1 + weaponDamageBonus;
                        if (zombie.HP <= 0)
                        {
                            deadZombies.Add(zombie);
                        }

                        gameCanvas.Children.Remove(bullet.Shape);
                        toRemove.Add(bullet);
                        break;
                    }
                }
            }

            foreach (var deadZombie in deadZombies)
            {
                ZombieDied(deadZombie);
            }

            bullets.RemoveAll(b => toRemove.Contains(b));

            if (zombies.Count == 0 && !exitOpened)
            {
                OpenExit();
                exitOpened = true;
            }
            foreach (var zombie in deadZombies)
            {
                zombies.Remove(zombie);
                gameCanvas.Children.Remove(zombie.Shape);
            }

            if (zombies.Count == 0 && (upgradeBox == null || !upgradeBox.IsActive) && !roomsWithUpgradeBox.Contains(currentRoomIndex))
            {
                AddUpgradeBox();
                roomsWithUpgradeBox.Add(currentRoomIndex);
            }
        }

        private void OpenExit()
        {
            int[,] layout = rooms[currentRoomIndex].Layout;

            foreach (var rect in gameCanvas.Children.OfType<Rectangle>().Where(r => r.Fill == Brushes.Gold).ToList())
                gameCanvas.Children.Remove(rect);

            for (int y = 0; y < layout.GetLength(0); y++)
            {
                for (int x = 0; x < layout.GetLength(1); x++)
                {
                    if (layout[y, x] == 2)
                    {
                        var img = new Image
                        {
                            Width = TileSize,
                            Height = TileSize,
                            Source = new BitmapImage(new Uri(@"C:\work\Курсова\RoguelikeWPF\Images\darkness.png", UriKind.Absolute)),
                            Stretch = Stretch.Fill
                        };
                        Canvas.SetLeft(img, x * TileSize);
                        Canvas.SetTop(img, y * TileSize);
                        gameCanvas.Children.Add(img);
                    }
                }
            }
        }
        private void SpawnAcidShot(Zombie zombie)
        {
            double zx = zombie.X * TileSize + TileSize / 2;
            double zy = zombie.Y * TileSize + TileSize / 2;

            double px = Canvas.GetLeft(player) + player.Width / 2;
            double py = Canvas.GetTop(player) + player.Height / 2;

            Vector direction = new Point(px, py) - new Point(zx, zy);

            if (direction.Length == 0)
                direction = new Vector(1, 0);

            direction.Normalize();

            Ellipse blob = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.LimeGreen
            };
            Canvas.SetLeft(blob, zx);
            Canvas.SetTop(blob, zy);
            gameCanvas.Children.Add(blob);

            acidBlobs.Add(new AcidBlob
            {
                Shape = blob,
                Direction = direction,
                Speed = 3
            });
        }
        private void UpdateAcidBlobs()
        {
            List<AcidBlob> toRemove = new List<AcidBlob>();

            foreach (var blob in acidBlobs)
            {
                double x = Canvas.GetLeft(blob.Shape);
                double y = Canvas.GetTop(blob.Shape);

                x += blob.Direction.X * blob.Speed;
                y += blob.Direction.Y * blob.Speed;

                Canvas.SetLeft(blob.Shape, x);
                Canvas.SetTop(blob.Shape, y);

                double blobWidth = 0, blobHeight = 0;
                if (blob.Shape is FrameworkElement bfe)
                {
                    blobWidth = bfe.Width;
                    blobHeight = bfe.Height;
                }
                Rect blobRect = new Rect(x, y, blobWidth, blobHeight);
                Rect playerRect = new Rect(Canvas.GetLeft(player), Canvas.GetTop(player), player.Width, player.Height);

                if (blobRect.IntersectsWith(playerRect))
                {
                    playerHP--;
                    UpdateHUD();

                    CreateAcidPool(x, y);

                    if (playerHP <= 0 && !isGameOver)
                    {
                        isGameOver = true;
                        MessageBox.Show("Тебе розплавило кислотою!");
                        GameOverToMenu();
                        return;
                    }
                    toRemove.Add(blob);
                    gameCanvas.Children.Remove(blob.Shape);
                    continue;
                }

                int tileX = (int)(x / TileSize);
                int tileY = (int)(y / TileSize);
                int rows = rooms[currentRoomIndex].Layout.GetLength(0);
                int cols = rooms[currentRoomIndex].Layout.GetLength(1);
                if (tileX < 0 || tileY < 0 || tileX >= cols || tileY >= rows || rooms[currentRoomIndex].Layout[tileY, tileX] == 1)
                {
                    toRemove.Add(blob);
                    gameCanvas.Children.Remove(blob.Shape);
                }
            }

            acidBlobs.RemoveAll(b => toRemove.Contains(b));
        }

        private void LeaveAcidPuddle(double x, double y)
        {
            Ellipse puddle = new Ellipse
            {
                Width = TileSize,
                Height = TileSize,
                Fill = Brushes.DarkOliveGreen,
                Opacity = 0.6
            };

            double puddleX = Math.Floor(x / TileSize) * TileSize;
            double puddleY = Math.Floor(y / TileSize) * TileSize;

            Canvas.SetLeft(puddle, puddleX);
            Canvas.SetTop(puddle, puddleY);
            gameCanvas.Children.Add(puddle);

        }


        private void UpdateProjectiles()
        {
            List<Projectile> toRemove = new List<Projectile>();

            foreach (var proj in projectiles)
            {
                double x = Canvas.GetLeft(proj.Shape);
                double y = Canvas.GetTop(proj.Shape);

                x += proj.Direction.X * proj.Speed;
                y += proj.Direction.Y * proj.Speed;

                Canvas.SetLeft(proj.Shape, x);
                Canvas.SetTop(proj.Shape, y);

                int tileX = (int)(x / TileSize);
                int tileY = (int)(y / TileSize);

                int rows = rooms[currentRoomIndex].Layout.GetLength(0);
                int cols = rooms[currentRoomIndex].Layout.GetLength(1);
                if (tileX < 0 || tileY < 0 || tileX >= cols || tileY >= rows ||
                    rooms[currentRoomIndex].Layout[tileY, tileX] == 1)
                {
                    proj.OnHitWall(this);
                    toRemove.Add(proj);
                    gameCanvas.Children.Remove(proj.Shape);
                    continue;
                }

                if (proj.OnHitPlayer(this))
                {
                    toRemove.Add(proj);
                    gameCanvas.Children.Remove(proj.Shape);
                    continue;
                }

                foreach (var z in zombies)
                {
                    double pWidth = 0, pHeight = 0;
                    if (proj.Shape is FrameworkElement pfe)
                    {
                        pWidth = pfe.Width;
                        pHeight = pfe.Height;
                    }
                    Rect pRect = new Rect(Canvas.GetLeft(proj.Shape), Canvas.GetTop(proj.Shape), pWidth, pHeight);
                    double zWidth = 0, zHeight = 0;
                    if (z.Shape is FrameworkElement zfe)
                    {
                        zWidth = zfe.Width;
                        zHeight = zfe.Height;
                    }
                    Rect zRect = new Rect(Canvas.GetLeft(z.Shape), Canvas.GetTop(z.Shape), zWidth, zHeight);

                    if (pRect.IntersectsWith(zRect) && proj.OnHitZombie(this, z))
                    {
                        toRemove.Add(proj);
                        gameCanvas.Children.Remove(proj.Shape);
                        break;
                    }
                }
            }

            projectiles.RemoveAll(p => toRemove.Contains(p));
        }

        private void CreateAcidPool(double x, double y)
        {
            var pool = new Rectangle
            {
                Width = TileSize,
                Height = TileSize,
                Fill = Brushes.Green,
                Opacity = 0.5
            };

            x = (int)(x / TileSize) * TileSize;
            y = (int)(y / TileSize) * TileSize;

            Canvas.SetLeft(pool, x);
            Canvas.SetTop(pool, y);
            gameCanvas.Children.Add(pool);

            acidPools.Add(new AcidPool
            {
                Shape = pool,
                X = x,
                Y = y,
                Lifetime = 100
            });
        }

        private void UpdateAcidPools()
        {
            List<AcidPool> expired = new List<AcidPool>();

            foreach (var pool in acidPools)
            {
                pool.Lifetime--;

                Rect poolRect = new Rect(pool.X, pool.Y, TileSize, TileSize);
                Rect playerRect = new Rect(Canvas.GetLeft(player), Canvas.GetTop(player), player.Width, player.Height);

                if (poolRect.IntersectsWith(playerRect))
                {
                    playerHP--;
                    UpdateHUD();

                    if (playerHP <= 0 && !isGameOver)
                    {
                        isGameOver = true;
                        MessageBox.Show("Тебе роз’їла кислота!");
                        GameOverToMenu();
                        return;
                    }

                    expired.Add(pool);
                    gameCanvas.Children.Remove(pool.Shape);
                }

                if (pool.Lifetime <= 0)
                {
                    expired.Add(pool);
                    gameCanvas.Children.Remove(pool.Shape);
                }
            }

            acidPools.RemoveAll(p => expired.Contains(p));
        }

        private void ClearRoomVisuals()
        {
            var fadeRects = gameCanvas.Children
                .OfType<Rectangle>()
                .Where(r => r.Fill == Brushes.Black)
                .ToList();

            gameCanvas.Children.Clear();

            foreach (var fade in fadeRects)
                gameCanvas.Children.Add(fade);

            zombies.Clear();
            bullets?.Clear();
            acidBlobs?.Clear();
            acidPools?.Clear();
            medkits?.Clear();
        }

    }
}