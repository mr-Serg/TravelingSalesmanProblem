using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SalesManTour
{
    // У програмі використано цінні поради Кізло Тараса щодо покращення коду.
    // Завдяки йому, зокрема, увімкнено подвійну буферизацію для побудови графіки.

    public partial class MainForm : Form
    {
        private Point[] towns;         // координати міст
        private ISolver<Tour> solver;  // сутність, що вміє розв'язати задачу
        private CancellationTokenSource cts; // маркер вимоги завершення обчислень

        private Random random;         // генератор випадкових чисел для координат і мутацій
        private Stopwatch stopwatch;   // секундомір вимірює тривалість роботи

        private bool isRunning;        // поля, що характеризують
        private bool isSolved;         //  стан програми

        private string lblGenerationsText; // поля для зберігання
        private string lblStopwatchText;   //  початкових написів
        private string lblLengthText;      //

        // конструювання та ...
        public MainForm()
        {
            InitializeComponent();
            towns = null;
            solver = null;
            cts = null;
            random = new Random();
            stopwatch = new Stopwatch();
            isRunning = false;
            isSolved = false;
        }
        // ... початкові налаштування форми
        private void MainForm_Load(object sender, EventArgs e)
        {
            // Задаємо подвійну буферизацію зображення панелі з картою;
            // доступ до захищеного члена класу Panel можна отримати
            // без наслідування засобами рефлексії.
            typeof(Control).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.SetProperty)
                    .SetValue(pnlMap, true);
            // буде легко відновити створені на етапі проектування написи
            lblGenerationsText = lblGenerations.Text;
            lblStopwatchText = lblStopwatch.Text;
            lblLengthText = lblLength.Text;
        }

        #region DRAWING
        // Для побудови графіки потрібен графічний контекст. 
        // Отримати його найлегше з аргумента події Paint, а для панелі з
        // подвійною буферизацією - це єдиний правильний спосіб.
        private void pnlMap_Paint(object sender, PaintEventArgs e)
        {
            Graphics canvas = e.Graphics;
            canvas.Clear(Color.White);
            Tour bestTour = solver?.Best();
            if (bestTour != null) DrawTour(canvas, bestTour);
            if (towns != null) DrawMap(canvas);
            if (isSolved) DrawTowns(canvas);
        }
        // Зображає маршрут - замкнену ламану
        private void DrawTour(Graphics canvas, Tour tour)
        {
            lblLength.Text = $"Довжина туру{tour.Length(),10:#.000}";
            canvas.DrawLines(Pens.Black, tour.Route());
        }
        // Зображає міста - множину кругів у заданих координатах
        private void DrawMap(Graphics canvas)
        {
            for (int i = 0; i < towns.Length; ++i)
            {
                Rectangle rectangle = new Rectangle(towns[i].X - 4, towns[i].Y - 4, 7, 7);
                canvas.DrawEllipse(Pens.Black, rectangle);
                canvas.FillEllipse(Brushes.White, rectangle);
            }
        }
        // Друкує порядкові номери міст
        private void DrawTowns(Graphics canvas)
        {
            Font font = new Font("Microsoft Sans Serif", 8);
            // початкове місто виділено червоним кольором
            canvas.DrawString("1", font, Brushes.Red, new Point(towns[0].X - 4, towns[0].Y + 3));
            // решта номерів - чорні
            for (int i = 1; i < towns.Length; ++i)
            {
                canvas.DrawString((i + 1).ToString(), font, Brushes.Black,
                    new Point(towns[i].X - 5, towns[i].Y + 3));
            }
        }
        #endregion

        #region UILogic

        // Готує нову карту заданого розміру
        private void btnGenerate_Click(object sender, EventArgs e)
        {
            isSolved = false;
            solver = null;    // важливо для повторних запусків алгоритму
            
            int x = pnlMap.ClientSize.Width - 10; // розмір карти обмежено розмірами панелі
            int y = pnlMap.ClientSize.Height - 10;

            int mapSize = (int)nudNumber.Value; // кількість міст задає користувач

            towns = new Point[mapSize];
            for (int i = 0; i < mapSize; ++i) // координати міст генеруємо випадково в межах панелі
            {
                towns[i] = new Point(random.Next(x) + 5, random.Next(y) + 5);
            }
            Tour.towns = towns;

            pnlMap.Invalidate(); // зобразить карту
            UpdateUIstate();     // зробить доступними налаштування генетичного алгоритму
            ResetLabels();       // вилучить з написів результати попереднього розв'язування
        }

        // Запускає процес відшукання розв'язку та відображення його кроків
        private async void btnStart_Click(object sender, EventArgs e)
        {
            isSolved = false;
            isRunning = true;

            pnlMap.Invalidate(); // зітре попередній розв'язок
            UpdateUIstate();     // зробить недоступним генерування
            ResetLabels();       // вилучить з написів результати попереднього розв'язування

            solver = MakeSolver(threadsAmount: (int)nudThreads.Value);
            await RunGeneticAlgorithmAsync(solver);

            isRunning = false;
            isSolved = true;
            UpdateUIstate();     // після відшукання розв'язку генерування карти знову доступне
        }

        // передає паралельному потоку обчислень вимогу зупинитися
        private void btnStop_Click(object sender, EventArgs e)
        {
            cts?.Cancel();
        }
        // повертає написи до стану, заданого під час проектування
        private void ResetLabels()
        {
            lblGenerations.Text = lblGenerationsText;
            lblStopwatch.Text = lblStopwatchText;
            lblLength.Text = lblLengthText;
        }
        // змінює доступність частин інтерфейсу відповідно до етапу виконання
        private void UpdateUIstate()
        {
            btnGenerate.Enabled = !isRunning;
            btnStart.Enabled = grbParameters.Enabled = towns != null && !isRunning;
        }
        #endregion

        // "робочі конячки"
        // приховує створення послідовного чи паралельного "розв'язувача"
        private ISolver<Tour> MakeSolver(int threadsAmount)
        {
            if (threadsAmount == 1)
            {
                return new SSolver(
                    populationSize: (int)nudSize.Value,
                    mutationCount: (int)nudMutate.Value,
                    routeMutationCount: (int)nudRotate.Value,
                    maxGeneration: (int)nudGenMax.Value * 1000);
            }
            else
            {
                return new PSolver(
                    threadsCount: threadsAmount,
                    populationSize: (int)nudSize.Value,
                    mutationCount: (int)nudMutate.Value,
                    routeMutationCount: (int)nudRotate.Value,
                    maxGeneration: (int)nudGenMax.Value * 1000);
            }
        }

        private async Task RunGeneticAlgorithmAsync(ISolver<Tour> solver)
        {
            // До запуску завдання необхідно приготуватися:

            // - створити маркер зупинки
            cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            // - з його допомогою можна задати дію після завершення
            //token.Register(UpdateUIstate);

            // - створити об'єкт-інформатор, що відображатиме хід обчислень
            Progress<int> progressHandler = new Progress<int>(value =>
            {
                lblStopwatch.Text = "Минуло " + stopwatch.Elapsed.ToString(@"mm\:ss\.ff");
                lblGenerations.Text = $"Перевірено {value} поколінь";
                pnlMap.Invalidate();
            });

            stopwatch.Restart();
            // Власне обчислення в окремому потоці та наступне після обчислень завдання
            await Task.Run(() => solver.Solve(token, progressHandler))
                .ContinueWith(task => pnlMap.Invalidate());
            stopwatch.Stop();
        }
    }
}
