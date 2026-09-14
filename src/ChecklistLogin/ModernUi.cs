using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ChecklistLogin
{
    public class AnnouncementSlide
    {
        public string id { get; set; }
        public string image { get; set; }
        public string title { get; set; }
        public int seconds { get; set; }
    }

    public partial class MainWindow
    {
        private const string DefaultSupportUrl = "https://helpdeskalunosenai.vercel.app/";
        private const string DefaultNotice = "Antes de começar, confira seu equipamento. Encontrou algum problema? Informe no checklist e abra um chamado pelo QR code.";
        private List<AnnouncementSlide> _slides = new List<AnnouncementSlide>();
        private int _slideIndex;
        private bool _carouselPaused;
        private DispatcherTimer _carouselTimer;
        private TextBlock _slideTitle, _slideCount, _supportTitle;
        private Image _qrImage;
        private Border _supportPanel;
        private FrameworkElement _welcome;
        private Button _pauseCarousel;
        private Brush Ink { get { return Brush("#142842"); } }
        private static SolidColorBrush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        private TextBlock Label(string text, double size, string color, bool bold)
        {
            return new TextBlock { Text = text, FontSize = size, Foreground = Brush(color), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
        }
        private Border Surface(UIElement child, Thickness padding)
        {
            return new Border { Background = Brushes.White, BorderBrush = Brush("#DFE6EF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = padding, Child = child };
        }
        private BitmapSource DecodePicture(string data)
        {
            if (string.IsNullOrEmpty(data) || (!data.StartsWith("data:image/png;base64,") && !data.StartsWith("data:image/jpeg;base64,"))) return null;
            using (var stream = new MemoryStream(Convert.FromBase64String(data.Substring(data.IndexOf(',') + 1)))) {
                var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 1400; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
            }
        }
        private BitmapSource ResourcePicture(string name)
        {
            using (var stream = typeof(MainWindow).Assembly.GetManifestResourceStream(name)) {
                if (stream == null) return null;
                var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
            }
        }
        private void ApplyModernAppearance()
        {
            _customTitle.Text = RemoteAppearance.Get("title", "Checklist de Equipamentos");
            _customSubtitle.Text = RemoteAppearance.Get("subtitle", "CUIDE DO SEU ESPAÇO. APROVEITE SUA AULA.");
            var accent = Brush(RemoteAppearance.Get("accent", "#164194"));
            _customSubtitle.Foreground = accent; _progressBarFill.Background = accent; _customPanel.BorderBrush = accent;
            _customNotice.Text = RemoteAppearance.Get("notice", DefaultNotice);
            _customPanel.Visibility = string.IsNullOrWhiteSpace(_customNotice.Text) ? Visibility.Collapsed : Visibility.Visible;
            try { _slides = RemoteAppearance.ValidateSlides(RemoteAppearance.Get("slides", "[]")); } catch { _slides = new List<AnnouncementSlide>(); }
            if (_slides.Count == 0 && !string.IsNullOrEmpty(RemoteAppearance.Get("image", ""))) _slides.Add(new AnnouncementSlide { image = RemoteAppearance.Get("image", ""), title = "Comunicado", seconds = 10 });
            _slideIndex = 0; ShowSlide();
            _supportTitle.Text = RemoteAppearance.Get("supportTitle", "Precisa de ajuda?");
            string link = RemoteAppearance.Get("supportUrl", DefaultSupportUrl);
            _qrImage.Source = null;
            try { _qrImage.Source = DecodePicture(RemoteAppearance.Get("qrImage", "")); } catch { }
            if (_qrImage.Source == null && link == DefaultSupportUrl) _qrImage.Source = ResourcePicture("helpdesk-qr.png");
            _supportPanel.Visibility = string.IsNullOrWhiteSpace(link) || _qrImage.Source == null ? Visibility.Collapsed : Visibility.Visible;
        }
        private void ShowSlide()
        {
            _carouselTimer.Stop(); _customImage.Source = null;
            if (_slides.Count > 0) {
                _slideIndex = (_slideIndex + _slides.Count) % _slides.Count;
                try { _customImage.Source = DecodePicture(_slides[_slideIndex].image); } catch { }
                _slideTitle.Text = _slides[_slideIndex].title;
                _slideCount.Text = (_slideIndex + 1) + " / " + _slides.Count;
                _carouselTimer.Interval = TimeSpan.FromSeconds(Math.Max(5, Math.Min(60, _slides[_slideIndex].seconds)));
                if (_slides.Count > 1 && !_carouselPaused && IsVisible) _carouselTimer.Start();
            } else {
                _slideTitle.Text = "Bem-vindo ao seu espaço de aprendizagem"; _slideCount.Text = "SENAI · " + (_config.Location ?? "PORTO");
            }
            _welcome.Visibility = _customImage.Source == null ? Visibility.Visible : Visibility.Collapsed;
        }
        private Button CarouselButton(string label, string tooltip, RoutedEventHandler action)
        {
            var button = new Button { Content = label, Width = 29, Height = 27, FontSize = 13, Margin = new Thickness(2), Cursor = Cursors.Hand, ToolTip = tooltip };
            StyleButtonUnselected(button, "#FFFFFF", "#627890", "#E1E7EF"); button.Click += action; return button;
        }
        private void InitModernUi()
        {
            Title = "Checklist SENAI"; FontFamily = new FontFamily("Segoe UI"); WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; Topmost = true; ShowInTaskbar = true; Background = Brush("#F1F5F9");
            PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if ((e.Key == Key.System && e.SystemKey == Key.F4) || e.Key == Key.Escape) e.Handled = true; };
            _carouselTimer = new DispatcherTimer(); _carouselTimer.Tick += delegate { _slideIndex++; ShowSlide(); };
            IsVisibleChanged += delegate { if (IsVisible) ShowSlide(); else _carouselTimer.Stop(); };
            Closed += delegate { _carouselTimer.Stop(); };
            var root = new Grid { Background = Brush("#F1F5F9") };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new Grid { Margin = new Thickness(30, 18, 30, 18) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(146) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var logo = new Image { Source = ResourcePicture("logo.png"), Height = 44, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 24, 0) }; header.Children.Add(logo);
            var heading = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 20, 0) };
            _customSubtitle = Label("", 9, "#164194", true); _customTitle = Label("", 23, "#142842", true); _customTitle.Margin = new Thickness(0, 5, 0, 0);
            heading.Children.Add(_customSubtitle); heading.Children.Add(_customTitle); Grid.SetColumn(heading, 1); header.Children.Add(heading);
            var identity = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = 190 };
            identity.Children.Add(Label(_config.Location ?? "PORTO", 11, "#164194", true));
            identity.Children.Add(Label(Environment.UserName + " · " + Environment.MachineName, 10, "#718198", false)); Grid.SetColumn(identity, 2); header.Children.Add(identity);
            root.Children.Add(new Border { Child = header, Background = Brushes.White, BorderBrush = Brush("#E1E7EF"), BorderThickness = new Thickness(0, 0, 0, 1) });

            var body = new Grid { Margin = new Thickness(30, 22, 30, 22) };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            body.SizeChanged += delegate { body.ColumnDefinitions[2].Width = new GridLength(Math.Max(210, Math.Min(480, (body.ActualHeight - 76) * 9.0 / 16.0 + 2))); };
            var left = new Grid(); left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            section.Children.Add(Label("Vamos conferir seu equipamento?", 18, "#142842", true)); section.Children.Add(Label("Responda todos os itens para começar sua aula.", 11, "#748399", false)); left.Children.Add(section);
            var cards = new StackPanel();
            AddQuestion(cards, "tela", "Tela / Monitor", "A imagem está sendo exibida corretamente?");
            AddQuestion(cards, "teclado", "Teclado", "Todas as teclas estão funcionando?");
            bool notebook = (_config.Location ?? "").ToUpperInvariant().Contains("NOTEBOOK");
            AddQuestion(cards, notebook ? "touchpad" : "mouse", notebook ? "Touchpad / Mouse" : "Mouse", "Os cliques e a navegação estão funcionando?");
            AddQuestion(cards, "internet", "Internet / Rede", "A conexão com a rede está funcionando?");
            if (!notebook) AddQuestion(cards, "computador", "Computador", "Liga sem ruídos ou lentidão?");
            var questionScroll = new ScrollViewer { Content = cards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            questionScroll.SizeChanged += delegate { foreach (var q in _questions) q.CardBorder.MinHeight = Math.Max(64, (questionScroll.ActualHeight / _questions.Count) - 9); };
            Grid.SetRow(questionScroll, 1); left.Children.Add(questionScroll);
            var tip = Label("Seu cuidado mantém o laboratório pronto para todos.", 10, "#8190A4", false); tip.Margin = new Thickness(4, 8, 0, 0); Grid.SetRow(tip, 2); left.Children.Add(tip); body.Children.Add(left);

            var right = new Grid(); right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var carousel = new Grid(); carousel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); carousel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); carousel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var top = new DockPanel { Margin = new Thickness(16, 12, 16, 12) }; _slideCount = Label("", 9, "#627890", true); _slideCount.HorizontalAlignment = HorizontalAlignment.Right; DockPanel.SetDock(_slideCount, Dock.Right); top.Children.Add(_slideCount); top.Children.Add(Label("FIQUE POR DENTRO", 9, "#164194", true)); carousel.Children.Add(top);
            var media = new Grid { Background = Brush("#EAF0F7"), ClipToBounds = true };
            var welcomeStack = new StackPanel { Width = 240, Margin = new Thickness(28), VerticalAlignment = VerticalAlignment.Center };
            welcomeStack.Children.Add(Label("APRENDER. CRIAR. TRANSFORMAR.", 9, "#9CD7FF", true));
            var welcomeTitle = Label("Seu próximo passo\ncomeça aqui.", 32, "#FFFFFF", true); welcomeTitle.Margin = new Thickness(0, 15, 0, 12); welcomeStack.Children.Add(welcomeTitle);
            welcomeStack.Children.Add(Label("Tecnologia, conhecimento e novas possibilidades em cada aula.", 12, "#CEE5FF", false));
            _welcome = new Border { Background = new LinearGradientBrush(Color.FromRgb(18, 59, 115), Color.FromRgb(22, 108, 170), 30), Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = welcomeStack } };
            media.Children.Add(_welcome); _customImage = new Image { Stretch = Stretch.Uniform }; media.Children.Add(_customImage); Grid.SetRow(media, 1); carousel.Children.Add(media);
            var bottom = new DockPanel { Margin = new Thickness(12, 6, 12, 6) };
            var controls = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(controls, Dock.Right);
            controls.Children.Add(CarouselButton("‹", "Foto anterior", delegate { _slideIndex--; ShowSlide(); }));
            _pauseCarousel = CarouselButton("Ⅱ", "Pausar ou reproduzir", delegate { _carouselPaused = !_carouselPaused; _pauseCarousel.Content = _carouselPaused ? "▶" : "Ⅱ"; ShowSlide(); }); controls.Children.Add(_pauseCarousel);
            controls.Children.Add(CarouselButton("›", "Próxima foto", delegate { _slideIndex++; ShowSlide(); })); bottom.Children.Add(controls);
            _slideTitle = Label("", 10, "#334B65", true); _slideTitle.VerticalAlignment = VerticalAlignment.Center; _slideTitle.TextWrapping = TextWrapping.NoWrap; _slideTitle.TextTrimming = TextTrimming.CharacterEllipsis; bottom.Children.Add(_slideTitle); Grid.SetRow(bottom, 2); carousel.Children.Add(bottom);
            var carouselSurface = Surface(carousel, new Thickness(0)); carouselSurface.ClipToBounds = true; right.Children.Add(carouselSurface);
            var noticeStack = new StackPanel(); noticeStack.Children.Add(Label("INFORMATIVO", 9, "#164194", true)); _customNotice = Label("", 11, "#334B65", false); _customNotice.Margin = new Thickness(0, 4, 0, 0); noticeStack.Children.Add(_customNotice);
            _customPanel = new Border { Background = Brush("#E7EEF9"), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 12, 0, 0), Child = new ScrollViewer { MaxHeight = 70, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = noticeStack } }; Grid.SetRow(_customPanel, 1);
            var support = new Grid(); support.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(114) }); support.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _qrImage = new Image { Width = 102, Height = 102, Stretch = Stretch.Uniform }; support.Children.Add(_qrImage);
            var supportText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) }; supportText.Children.Add(Label("SUPORTE AO ALUNO", 9, "#8393A6", true)); _supportTitle = Label("", 19, "#142842", true); supportText.Children.Add(_supportTitle); supportText.Children.Add(Label("Aponte a câmera do celular e abra seu chamado.", 11, "#718096", false)); var supportHint = Label("Abrir chamado  →", 11, "#164194", true); supportHint.Margin = new Thickness(0, 8, 0, 0); supportText.Children.Add(supportHint); Grid.SetColumn(supportText, 1); support.Children.Add(supportText);
            _supportPanel = Surface(support, new Thickness(14, 8, 14, 8)); _supportPanel.Margin = new Thickness(0, 12, 0, 0); Grid.SetRow(_supportPanel, 2);
            var information = new Grid(); information.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); information.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _customPanel.Margin = new Thickness(0, 12, 12, 0); _customPanel.VerticalAlignment = VerticalAlignment.Stretch;
            Grid.SetRow(_customPanel, 0); information.Children.Add(_customPanel);
            Grid.SetRow(_supportPanel, 0); Grid.SetColumn(_supportPanel, 1); information.Children.Add(_supportPanel);
            Grid.SetRow(information, 3); left.Children.Add(information);
            Grid.SetColumn(right, 2); body.Children.Add(right); Grid.SetRow(body, 1); root.Children.Add(body);

            var footer = new Grid { Margin = new Thickness(30, 12, 30, 12) }; footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var footerText = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; _progressText = Label("Responda todos os itens para continuar", 12, "#334B65", true); footerText.Children.Add(_progressText); footerText.Children.Add(Label("As respostas ajudam a cuidar do seu laboratório.", 10, "#8393A6", false));
            _progressBarFill = new Border { Height = 3, Width = 0, HorizontalAlignment = HorizontalAlignment.Left, Background = Brush("#164194"), Margin = new Thickness(0, 5, 0, 0) }; footerText.Children.Add(_progressBarFill); footer.Children.Add(footerText);
            _btnSubmit = new Button { Content = "Concluir checklist  →", Height = 46, Width = 240, FontSize = 13, FontWeight = FontWeights.SemiBold, IsEnabled = false, Cursor = Cursors.No }; UpdateSubmitButtonStyle(); _btnSubmit.Click += BtnSubmit_Click; Grid.SetColumn(_btnSubmit, 1); footer.Children.Add(_btnSubmit);
            var footerSurface = new Border { Background = Brushes.White, BorderBrush = Brush("#DFE6EF"), BorderThickness = new Thickness(0, 1, 0, 0), Child = footer }; Grid.SetRow(footerSurface, 2); root.Children.Add(footerSurface);
            Content = root;
        }
    }
}
