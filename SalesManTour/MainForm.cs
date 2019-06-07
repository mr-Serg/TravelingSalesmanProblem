using System;

using System.Diagnostics;

using System.Threading;
using System.Threading.Tasks;

using System.Drawing;
using System.Windows.Forms;

namespace SalesManTour
{
    public partial class MainForm : Form
    {
        // FIELDS
        private bool isSolved;
        private bool isRunning;
        private bool doRegenerateMap;

        private Point[] towns;
        private ISolver<Tour> solver;
        
        private Random random;
        private CancellationTokenSource cts;

        private Stopwatch stopwatch;

        // CONSTRUCTORS
        public MainForm()
        {
            InitializeComponent();

            // turn on double buffered for panel
            // to avoid glimmering
            typeof(Control).GetProperty("DoubleBuffered", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance  | 
                System.Reflection.BindingFlags.SetProperty)
                    .SetValue(pnlMap, true, null);

            UpdateInterface();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // initialize fields
            isSolved  = false;
            isRunning = false;
            doRegenerateMap = reGenCb.CheckState == CheckState.Checked;

            towns  = null;
            solver = null;

            random = new Random();
            cts = null;

            stopwatch = new Stopwatch();
        }
        private void UpdateInterface()
        {
            reGenCb.Enabled = !isRunning;
            btnGenerate.Enabled = !isRunning;
            btnStart.Enabled = towns != null && !isRunning;
            btnStop.Enabled = isRunning;
        }
        // DRAWING
        #region DRAWING
        private void Redraw()
        {
            pnlMap.Invalidate();
        }
        // handle event Paint
        // Controls in Windows Forms are redraw in separate thread
        // in this case we do not block user interface
        private void pnlMap_Paint(object sender, PaintEventArgs e)
        {
            Graphics canvas = e.Graphics;

            Clear(canvas);

            Tour bestTour = solver?.Best();
            if (bestTour != null)            DrawTour(canvas, bestTour);
            if (towns != null)               DrawMap(canvas);
            if (towns != null && isSolved)   DrawTowns(canvas);
        }
        private void Clear(Graphics graphics)
        {
            graphics.Clear(Color.White);
        }
        private void DrawMap(Graphics canvas)
        {
            for (int i = 0; i < towns.Length; ++i)
            {
                Rectangle town = new Rectangle(towns[i].X - 4, towns[i].Y - 4, 7, 7);
                canvas.DrawEllipse(Pens.Black, town);
                canvas.FillEllipse(Brushes.White, town);
            }
        }
        private void DrawTowns(Graphics canvas)
        {
            Font font = new Font("Microsoft Sans Serif", 8);

            // first town
            canvas.DrawString("1", font, Brushes.Red, new Point(towns[0].X - 4, towns[0].Y + 3));
            canvas.FillEllipse(Brushes.Red, new Rectangle(towns[0].X - 4, towns[0].Y -4, 7, 7));
            
            // others town 
            for (int i = 1; i < towns.Length; ++i)
            {
                canvas.DrawString((i + 1).ToString(), font, Brushes.Black,
                    new Point(towns[i].X - 5, towns[i].Y + 3));
            }
        }
        private void DrawTour(Graphics canvas, Tour t)
        {
            lblLength.Text = $"Довжина туру{t.Length(),10:#.000}";
            canvas.DrawLines(Pens.Black, t.Route());
        }
        #endregion

        // INTERFACE LOGIC
        private void BtnGenerate_Click(object sender, EventArgs e)
        {
            // get input data
            isSolved = false;

            int x = pnlMap.ClientSize.Width-10;
            int y = pnlMap.ClientSize.Height-10;
            int mapSize = (int)nudNumber.Value;

            // create randpm towns
            towns = new Point[mapSize];
            for (int i = 0; i < towns.Length; ++i)
            {
                towns[i] = new Point(random.Next(x) + 5, random.Next(y) + 5);
            }
            Tour.towns = towns;

            // reset solver
            solver = null;

            // update view
            Redraw();
            UpdateInterface();
            // clear genetic algorithms result
            lblStopwatch.Text = "Минуло";
            lblGenerations.Text = "Перевірено 0 поколінь";
            lblLength.Text = "Довжина туру";
        }
        

        private async void btnStart_Click(object sender, EventArgs e)
        {
            // run and update view
            isRunning = true;
            UpdateInterface();

            // start genetic algorithm
            stopwatch.Restart();

            solver = MakeSolver(threadsAmount: (int)nudThreads.Value);
            await RunGeneticAlgorithmAsync(solver);

            // end genetic algorithm
            stopwatch.Stop();
            isRunning = false;

            // update view
            Redraw();
            UpdateInterface();
        }
        private async Task RunGeneticAlgorithmAsync(ISolver<Tour> solver)
        {
            isSolved = false;

            // create NEW cancelation source
            cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            token.Register(UpdateInterface);

            // track progress ..
            Progress<int> progressHandler = new Progress<int>(value =>
            {
                // .. and show it
                lblStopwatch.Text = "Минуло " + stopwatch.Elapsed.ToString(@"mm\:ss\.ff");
                lblGenerations.Text = $"Перевірено {value} поколінь";

                // do not block user interface
                // let Forms update interface in its onw thread
                Redraw();
            });

            // run solver
            await Task.Run(() => 
            {
                solver.Solve(token, progressHandler);
            });

            isSolved  = true;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            // cancel
            cts?.Cancel();
            stopwatch.Stop();
            
            // update field
            isSolved  = true;
            isRunning = false;
        }

        private void MainForm_ResizeEnd(object sender, EventArgs e)
        {
            if (doRegenerateMap && !isRunning)
            {
                btnGenerate.PerformClick();
            }
        }

        private void reGenCb_CheckedChanged(object sender, EventArgs e)
        {
            doRegenerateMap = reGenCb.CheckState == CheckState.Checked;
        }

        // almost a factory
        private ISolver<Tour> MakeSolver(int threadsAmount)
        {
            if (threadsAmount == 1)
            {
                return new SSolver(
                    populationSize:     (int)nudSize.Value,
                    mutationCount:      (int)nudMutate.Value,
                    routeMutationCount: (int)nudRotate.Value,
                    maxGeneration:      (int)nudGenMax.Value * 1000);
            }
            else
            {
                return new PSolver(
                    threadsCount:       threadsAmount,
                    populationSize:     (int)nudSize.Value,                    
                    mutationCount:      (int)nudMutate.Value,
                    routeMutationCount: (int)nudRotate.Value,
                    maxGeneration:      (int)nudGenMax.Value * 1000);
            }
        }

    }
}
