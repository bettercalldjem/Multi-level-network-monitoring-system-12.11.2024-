using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using LiveCharts;
using LiveCharts.Wpf;
using System.Net;

namespace NetworkMonitor
{
    public partial class MainWindow : Window
    {
        private List<ServerInfo> _servers = new List<ServerInfo>();
        private DispatcherTimer _timer;

        public MainWindow()
        {
            InitializeComponent();
            LoadServers();
            SetupTimer();
            SetupChart();
        }

        // Загружаем список серверов из файла
        private void LoadServers()
        {
            try
            {
                if (File.Exists("servers.txt"))
                {
                    var lines = File.ReadAllLines("servers.txt");
                    foreach (var line in lines)
                    {
                        var parts = line.Split(',');
                        if (parts.Length == 2 && Uri.IsWellFormedUriString(parts[0], UriKind.Absolute) && int.TryParse(parts[1], out int interval))
                        {
                            _servers.Add(new ServerInfo { IpAddress = parts[0], CheckInterval = interval });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке серверов: {ex.Message}");
            }

            ServersDataGrid.ItemsSource = _servers;
        }

        // Настройка таймера для периодической проверки серверов
        private void SetupTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(30);
            _timer.Tick += async (sender, e) => await CheckServers();
            _timer.Start();
        }

        // Проверка всех серверов
        private async Task CheckServers()
        {
            foreach (var server in _servers)
            {
                var status = await PingServerAsync(server.IpAddress);
                server.Status = status ? "В сети" : "Не в сети";
                server.LastResponseTime = DateTime.Now.ToString("HH:mm:ss");
                server.LastChecked = DateTime.Now;
                LogStatus(server);
            }

            // Обновляем UI
            ServersDataGrid.Items.Refresh();
            UpdateChart();
        }

        // Пинг сервера с использованием async/await
        private async Task<bool> PingServerAsync(string ipAddress)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(ipAddress, 5000); // Таймаут 5 секунд
                    return reply.Status == IPStatus.Success;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Логирование статуса
        private void LogStatus(ServerInfo server)
        {
            try
            {
                File.AppendAllText("log.txt", $"{DateTime.Now}: {server.IpAddress} - {server.Status}\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка записи в лог: {ex.Message}");
            }
        }

        // Обработчик нажатия кнопки добавления сервера
        private void AddServerButton_Click(object sender, RoutedEventArgs e)
        {
            var ipAddress = IpAddressTextBox.Text;
            var intervalText = IntervalTextBox.Text;
            if (IPAddress.TryParse(ipAddress, out _) && int.TryParse(intervalText, out int interval) && interval >= 1 && interval <= 10)
            {
                _servers.Add(new ServerInfo { IpAddress = ipAddress, CheckInterval = interval });
                ServersDataGrid.Items.Refresh();
                SaveServers();
            }
            else
            {
                MessageBox.Show("Неверный формат IP-адреса или интервала.");
            }
        }

        // Обработчик нажатия кнопки удаления сервера
        private void RemoveServerButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedServer = (ServerInfo)ServersDataGrid.SelectedItem;
            if (selectedServer != null)
            {
                _servers.Remove(selectedServer);
                ServersDataGrid.Items.Refresh();
                SaveServers();
            }
        }

        // Сохранение списка серверов в файл
        private void SaveServers()
        {
            try
            {
                var serverData = _servers.Select(s => $"{s.IpAddress},{s.CheckInterval}").ToArray();
                File.WriteAllLines("servers.txt", serverData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения серверов: {ex.Message}");
            }
        }

        // Фильтрация серверов по состоянию
        private void StatusFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedStatus = ((ComboBoxItem)StatusFilterComboBox.SelectedItem).Content.ToString();
            if (selectedStatus == "Все")
            {
                ServersDataGrid.ItemsSource = _servers;
            }
            else
            {
                ServersDataGrid.ItemsSource = _servers.Where(s => s.Status == selectedStatus).ToList();
            }
        }

        // Настройка круговой диаграммы для отображения доступности серверов
        private void SetupChart()
        {
            NetworkAvailabilityChart.Series = new SeriesCollection
            {
                new PieSeries
                {
                    Title = "В сети",
                    Values = new ChartValues<int> { 0 },
                    DataLabels = true
                },
                new PieSeries
                {
                    Title = "Не в сети",
                    Values = new ChartValues<int> { 0 },
                    DataLabels = true
                }
            };
        }

        // Обновление данных диаграммы
        private void UpdateChart()
        {
            var availableCount = _servers.Count(s => s.Status == "В сети");
            var unavailableCount = _servers.Count(s => s.Status == "Не в сети");

            ((PieSeries)NetworkAvailabilityChart.Series[0]).Values = new ChartValues<int> { availableCount };
            ((PieSeries)NetworkAvailabilityChart.Series[1]).Values = new ChartValues<int> { unavailableCount };
        }
    }

    // Класс для хранения информации о сервере
    public class ServerInfo
    {
        public string IpAddress { get; set; }
        public string Status { get; set; }
        public string LastResponseTime { get; set; }
        public DateTime LastChecked { get; set; }
        public int CheckInterval { get; set; }
    }
}
