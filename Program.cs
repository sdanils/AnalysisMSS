using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using ScottPlot;
using System.Drawing;
using System.Security.Principal;
using System.Runtime.InteropServices;


namespace TPProj
{
    class Program
    {
        static int T = 100;
        static void Main()
        {
            List<List<double>> statistic = new List<List<double>>();

            for (double intensityApplications = 1.0; intensityApplications < 7; intensityApplications += 0.5)
            {
                statistic.Add(RunExperiment(intensityApplications));
            }

            List<string> parametrs = new List<string> { "Вероятность простоя", "Вероятность отказа", "Относительная пропускная", "Абсолютная пропускная", "Среднее число занятых каналов" };
            double[] x = statistic.Select(row => row[0]).ToArray();
            for (int i = 1; i < 6; i++)
            {
                double[] y1 = statistic.Select(row => row[i]).ToArray();
                double[] y2 = statistic.Select(row => row[i + 5]).ToArray();
                string param = parametrs[i - 1];
                string title = $"Зависимость {param} от интенсивности заявок";
                string fileName = $"result/p-{i}.png";
                CreatePlot(x, y1, y1, title, fileName, param);
            }
        }
        static List<double> RunExperiment(double intensityApplications, int numberThread = 10, int numberApplications = 100, double serviceIntensity = 4.0)
        {
            Server server = new Server(numberThread, serviceIntensity, T);
            Client client = new Client(server);

            int applicationDelay = (int)(T / intensityApplications);
            for (int id = 1; id <= numberApplications; id++)
            {
                client.send(id);
                Thread.Sleep(applicationDelay);
            }

            var result = GenStatistic(server, intensityApplications, serviceIntensity, numberApplications, numberThread);
            return result;
        }
        static double Factorial(int x) => x == 0 ? 1 : x * Factorial(x - 1);
        static List<double> GenStatistic(Server server, double lambda, double mu, int numberApplications, int numberThread)
        {
            List<double> results = new List<double>();
            results.Add(lambda);

            double p = lambda / mu;
            double sum = 0;
            for (int j = 0; j < numberThread; j++)
                sum += Math.Pow(p, j) / Factorial(j);
            double P0t = 1L / sum;
            results.Add(P0t);
            double Pnt = P0t * Math.Pow(p, numberThread) / Factorial(numberThread);
            results.Add(Pnt);
            double Qt = 1 - Pnt;
            results.Add(Qt);
            double At = lambda * Qt;
            results.Add(At);
            double kt = At / mu;
            results.Add(kt);

            double P0 = 1 - (double)server.processedCount / server.requestCount;
            results.Add(P0);
            double Pn = (double)server.rejectedCount / server.requestCount;
            results.Add(Pn);
            double Q = (double)server.processedCount / server.requestCount;
            results.Add(Q);
            double totalTime = (T / lambda) *numberApplications / T;
            double A = lambda * (double)server.processedCount / totalTime;
            results.Add(A);
            double k = A / mu;
            results.Add(k);

            return results;
        }
        static void CreatePlot(double[] x, double[] y1, double[] y2, string title, string filename, string parametrName)
        {
            var plot = new ScottPlot.Plot();

            var line1 = plot.Add.Scatter(x, y1);
            line1.LegendText = $"{parametrName} теоретическая(ый)";
            line1.Color = Colors.Blue;

            var line2 = plot.Add.Scatter(x, y2);
            line2.LegendText = $"{parametrName} эксперементальная(ый)";
            line2.Color = Colors.Red;

            plot.ShowLegend();
            plot.Title(title);
            plot.YLabel($"{parametrName}");
            plot.XLabel($"Интенсивность потока заявок");

            plot.SavePng(filename, 1000, 800);
        }
    }
    struct PoolRecord
    {
        public Thread thread;
        public bool in_use;
    }
    class Server
    {
        private PoolRecord[] pool;
        private object threadLock = new object();
        public int requestCount = 0;
        public int processedCount = 0;
        public int rejectedCount = 0;
        public int numberProcces = 0;
        public Server(int numberThread, double serviceIntensity, int T)
        {
            pool = new PoolRecord[numberThread];
            this.numberProcces = (int)Math.Round(T / serviceIntensity);
        }
        public void proc(object sender, procEventArgs e)
        {
            lock (threadLock)
            {
                Console.WriteLine($"Заявка с номером: {e.id}");
                requestCount++;
                for (int i = 0; i < pool.Length; i++)
                {
                    if (!pool[i].in_use)
                    {
                        pool[i].in_use = true;
                        pool[i].thread = new Thread(new ParameterizedThreadStart(Answer!));
                        pool[i].thread.Start(e.id);
                        processedCount++;
                        return;
                    }
                }
                rejectedCount++;
            }
        }
        public void Answer(object arg)
        {
            int id = (int)arg;
            Console.WriteLine($"Обработка заявки: {id}");
            Thread.Sleep(numberProcces);

            for (int i = 0; i < pool.Length; i++)
                if (pool[i].thread == Thread.CurrentThread)
                    pool[i].in_use = false;
        }
    }
    class Client
    {
        private Server server;
        public Client(Server server)
        {
            this.server = server;
            this.request += server.proc!;
        }
        public void send(int id)
        {
            procEventArgs args = new procEventArgs();
            args.id = id;
            OnProc(args);
        }
        protected virtual void OnProc(procEventArgs e)
        {
            EventHandler<procEventArgs> handler = request;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        public event EventHandler<procEventArgs> request;
    }
    public class procEventArgs : EventArgs
    {
        public int id { get; set; }
    }
}