using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ChecklistLogin;

class RenderChecklist {
    [STAThread]
    static int Main(string[] args) {
        var app = new Application();
        var window = new MainWindow(null, true, args.Length > 3 ? args[3] : "PORTO");
        RemoteAppearance.Current = new Dictionary<string,string>();
        typeof(MainWindow).GetMethod("ApplyAppearance", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
        var root = (FrameworkElement)window.Content;
        int width = args.Length > 1 ? int.Parse(args[1]) : 1366;
        int height = args.Length > 2 ? int.Parse(args[2]) : 768;
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(args[0])) png.Save(stream);
        var questions = (List<ChecklistQuestion>)typeof(MainWindow).GetField("_questions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        if (questions.Count != ((args.Length > 3 && args[3].Contains("NOTEBOOK")) ? 4 : 5)) throw new Exception("Desktop must have five questions");
        foreach (var q in questions) q.BtnSim.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var submit = (Button)typeof(MainWindow).GetField("_btnSubmit", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        if (!submit.IsEnabled) throw new Exception("Complete answers should enable submit");
        questions[0].BtnNao.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (submit.IsEnabled) throw new Exception("A problem needs a description");
        questions[0].DescTextBox.Text = "Tela com defeito";
        if (!submit.IsEnabled) throw new Exception("Described problem should enable submit");
        typeof(MainWindow).GetMethod("ApplyAppearance", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
        if (!submit.IsEnabled || questions[0].ProblemDescription != "Tela com defeito") throw new Exception("Appearance refresh erased answers");
        string photo;
        using (var resource = typeof(MainWindow).Assembly.GetManifestResourceStream("logo.png"))
        using (var bytes = new MemoryStream()) { resource.CopyTo(bytes); photo = "data:image/png;base64," + Convert.ToBase64String(bytes.ToArray()); }
        RemoteAppearance.Current["slides"] = "[{\"id\":\"1\",\"title\":\"Foto 1\",\"image\":\"" + photo + "\",\"seconds\":5},{\"id\":\"2\",\"title\":\"Foto 2\",\"image\":\"" + photo + "\",\"seconds\":12}]";
        typeof(MainWindow).GetMethod("ApplyAppearance", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
        var carouselImage = (Image)typeof(MainWindow).GetField("_customImage", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
        if (carouselImage.Source == null) throw new Exception("Carousel photo did not render");
        typeof(MainWindow).GetField("_slideIndex", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window, 1);
        typeof(MainWindow).GetMethod("ShowSlide", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
        var caption = (TextBlock)typeof(MainWindow).GetField("_slideTitle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
        var timer = (System.Windows.Threading.DispatcherTimer)typeof(MainWindow).GetField("_carouselTimer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
        if (caption.Text != "Foto 2" || timer.Interval.TotalSeconds != 12) throw new Exception("Carousel order/duration failed");
        Console.WriteLine("Rendered " + width + "x" + height + "; checklist validation and answer preservation passed. No data was submitted.");
        return 0;
    }
}
