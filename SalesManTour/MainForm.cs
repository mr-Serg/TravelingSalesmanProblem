using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SalesManTour
{
    public partial class MainForm : Form
    {
        private int mapSize;
        private Point[] towns;
        private ISolver<Tour> solver;

        private Graphics canvas;
        private Random random;
        private CancellationTokenSource cts;

        private Stopwatch stopwatch;

        public MainForm()
        {
            InitializeComponent();
            towns = null;
            random = new Random();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            canvas = pnlMap.CreateGraphics();
            stopwatch = new Stopwatch();
            mapSize = (int)nudNumber.Value;
        }

        private void NudNumber_ValueChanged(object sender, EventArgs e)
        {
            mapSize = (int)nudNumber.Value;
        }

        private void BtnGenerate_Click(object sender, EventArgs e)
        {
            int x = pnlMap.ClientSize.Width-10;
            int y = pnlMap.ClientSize.Height-10;
            towns = new Point[mapSize];
            Tour.towns = towns;
            for (int i = 0; i < mapSize; ++i)
            {
                towns[i] = new Point(random.Next(x) + 5, random.Next(y) + 5);
            }
            canvas.Clear(Color.White);
            DrawMap();
            grbSolver.Enabled = true;
        }
        
        private void DrawMap()
        {
            for (int i = 0; i < mapSize; ++i)
            {
                Rectangle rectangle = new Rectangle(towns[i].X - 4, towns[i].Y - 4, 7, 7);
                canvas.DrawEllipse(Pens.Black, rectangle);
                canvas.FillEllipse(Brushes.White, rectangle);
            }
        }
        
        private void DrawTowns()
        {
            Font font = new Font("Microsoft Sans Serif", 8);
            canvas.DrawString("1", font, Brushes.Red, new Point(towns[0].X - 4, towns[0].Y + 3));
            for (int i = 1; i < mapSize; ++i)
            {
                canvas.DrawString((i + 1).ToString(), font, Brushes.Black,
                    new Point(towns[i].X - 5, towns[i].Y + 3));
            }
        }

        private void MainForm_ResizeEnd(object sender, EventArgs e)
        {
            canvas = pnlMap.CreateGraphics();
        }

        private void DrawTour(Tour t)
        {
            lblLength.Text = $"Довжина туру{t.Length(),10:#.000}";
            canvas.Clear(Color.White);
            canvas.DrawLines(Pens.Black, t.Route());
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            (sender as Button).Enabled = false;
            grbParameters.Enabled = false;
            stopwatch.Restart();
            int thrCount = (int)nudThreads.Value;
            if (thrCount == 1)
                solver = new SSolver((int)nudSize.Value, (int)nudMutate.Value,
                    (int)nudRotate.Value, (int)nudGenMax.Value * 1000);
            else
                solver = new PSolver(thrCount,
                    (int)nudSize.Value, (int)nudMutate.Value,
                    (int)nudRotate.Value, (int)nudGenMax.Value * 1000);
            cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            token.Register(() =>
            {
                grbParameters.Enabled = true;
                btnStart.Enabled = true;
            });
            Progress<int> progressHandler = new Progress<int>(value =>
            {
                lblStopwatch.Text = "Минуло " + stopwatch.Elapsed.ToString().Remove(12);
                lblGenerations.Text = $"Перевірено {value} поколінь";
                DrawTour(solver.Best());
                DrawMap();
            });
            await Task.Run(() => solver.Solve(token, progressHandler)).ContinueWith(task =>
            { Thread.Sleep(mapSize + 10); DrawTowns(); });
            stopwatch.Stop();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (cts != null) cts.Cancel();
            stopwatch.Stop();
        }
    }
}
