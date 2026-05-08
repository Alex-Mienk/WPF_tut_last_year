using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Serialization;

namespace FourierPlotterLab;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const double AnimationSeconds = 10.0;
    private readonly DispatcherTimer timer;
    private readonly Stopwatch stopwatch = new();
    private TimeSpan elapsedBeforePause = TimeSpan.Zero;
    private bool drawingIsReady;

    public ObservableCollection<CircleItem> Circles { get; } = new()
    {
        new CircleItem { Radius = 100, Frequency = 1 },
        new CircleItem { Radius = 60, Frequency = -2 },
        new CircleItem { Radius = 35, Frequency = 3 }
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(20)
        };
        timer.Tick += Timer_Tick;
        Circles.CollectionChanged += (_, _) => RedrawCircleIfReady();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (Progress.Value >= Progress.Maximum)
        {
            ResetProcedure();
        }

        stopwatch.Restart();
        timer.Start();
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (timer.IsEnabled)
        {
            elapsedBeforePause += stopwatch.Elapsed;
            stopwatch.Reset();
            timer.Stop();
            PauseButton.Content = "Resume";
            return;
        }

        stopwatch.Restart();
        timer.Start();
        PauseButton.Content = "Pause";
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ResetProcedure();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        TimeSpan elapsed = elapsedBeforePause + stopwatch.Elapsed;
        double progress = elapsed.TotalSeconds / AnimationSeconds * 100.0;
        Progress.Value = Math.Min(progress, Progress.Maximum);

        if (Progress.Value < Progress.Maximum)
        {
            return;
        }

        timer.Stop();
        stopwatch.Reset();
        elapsedBeforePause = TimeSpan.Zero;
        drawingIsReady = true;
        PauseButton.Content = "Pause";
        DrawFirstCircle();
    }

    private void NewMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Circles.Clear();
        ResetProcedure();
    }

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            XmlSerializer serializer = new(typeof(List<CircleItem>));
            using FileStream stream = File.OpenRead(dialog.FileName);
            List<CircleItem>? loadedCircles = serializer.Deserialize(stream) as List<CircleItem>;

            Circles.Clear();
            foreach (CircleItem circle in loadedCircles ?? [])
            {
                Circles.Add(circle);
            }

            ResetProcedure();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not open the file: {ex.Message}", "Open failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SaveFileDialog dialog = new()
        {
            Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
            DefaultExt = ".xml"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        XmlSerializer serializer = new(typeof(List<CircleItem>));
        using FileStream stream = File.Create(dialog.FileName);
        serializer.Serialize(stream, Circles.ToList());
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
    }

    private void CirclesDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(RedrawCircleIfReady, DispatcherPriority.Background);
    }

    private void RedrawCircleIfReady()
    {
        if (drawingIsReady)
        {
            DrawFirstCircle();
        }
    }

    private void DrawFirstCircle()
    {
        double width = Math.Max(PlotImage.ActualWidth, 1);
        double height = Math.Max(PlotImage.ActualHeight, 1);
        double radius = Circles.FirstOrDefault()?.Radius ?? 0;

        DrawingVisual visual = new();
        using (DrawingContext context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

            if (radius > 0)
            {
                Point center = new(width / 2.0, height / 2.0);
                Pen circlePen = new(Brushes.SteelBlue, 3);
                context.DrawEllipse(null, circlePen, center, radius, radius);
            }
        }

        RenderTargetBitmap bitmap = new(
            (int)Math.Ceiling(width),
            (int)Math.Ceiling(height),
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        PlotImage.Source = bitmap;
    }

    private void ResetProcedure()
    {
        timer.Stop();
        stopwatch.Reset();
        elapsedBeforePause = TimeSpan.Zero;
        drawingIsReady = false;
        Progress.Value = 0;
        PlotImage.Source = null;
        PauseButton.Content = "Pause";
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
