using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Media.Animation;

using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace CircularChartApp
{
    public partial class MainWindow : Window
    {
        private FontFamily defaultFont;

        private ScaleTransform scaleTransform;
        private TranslateTransform translateTransform;
        private TransformGroup transformGroup;
        private double currentScale = 1.0;
        private const double ScaleFactor = 1.1;
        private const double AnimationDurationMs = 200;


        private DispatcherTimer animationTimer;

        private DispatcherTimer redrawTimer;
        private bool isAnimating = false;

        private AstronomicalDate currentDate;

        private Point lastMousePosition;
        private bool isDragging = false;
        private Point startDragPoint;

        private const int DIVISIONS = 24;
        private const double ECLIPTIC_TILT = 23.26; // 黄道倾角

        private const double TOTAL_DEGREES = 365.25;
       
        private const int REFERENCE_YEAR = -2629; // 公元前2629年
        private const double PRECESSION_RATE = 1.0 / 72; // 每年岁差率（度/年）
        private const double REFERENCE_LONGITUDE = 240.0; // 牛宿一的参考黄经

        private const double ECLIPTIC_DEGREES = 360.0;

        private (string Name, double StartLongitude, double EndLongitude)[] mansionsWithLongitudes;



        private readonly (string Name, double Degrees)[] twentyEightMansions = new[]
        {
        // 东方七宿
        ("角", 12.0), ("亢", 9.0), ("氐", 15.0), ("房", 5.0), ("心", 5.0), ("尾", 18.0), ("箕", 11.0),
        // 北方七宿
        ("斗", 26.25), ("牛", 8.0), ("女", 12.0), ("虚", 10.0), ("危", 17.0), ("室", 16.0), ("壁", 9.0),
        // 西方七宿
        ("奎", 16.0), ("娄", 12.0), ("胃", 14.0), ("昴", 11.0), ("毕", 16.0), ("觜", 2.0), ("参", 9.0),
        // 南方七宿
        ("井", 33.0), ("鬼", 4.0), ("柳", 15.0), ("星", 7.0), ("张", 18.0), ("翼", 18.0), ("轸", 17.0)
    };


        private readonly (string Name, int Longitude)[] solarTerms = new[]
     {
        ("春分", 0), ("清明", 15), ("谷雨", 30), ("立夏", 45), ("小满", 60), ("芒种", 75),
        ("夏至", 90), ("小暑", 105), ("大暑", 120), ("立秋", 135), ("处暑", 150), ("白露", 165),
        ("秋分", 180), ("寒露", 195), ("霜降", 210), ("立冬", 225), ("小雪", 240), ("大雪", 255),
        ("冬至", 270), ("小寒", 285), ("大寒", 300), ("立春", 315), ("雨水", 330), ("惊蛰", 345)
    };

        private readonly (string Name, string Start, string End)[] twelveDivisions = new[]
         {
        ("星纪", "斗宿11度", "女宿7度"),
        ("玄枵", "女宿7度", "危宿15度"),
        ("娵訾", "危宿15度", "奎宿4度"),
        ("降娄", "奎宿4度", "胃宿6度"),
        ("大梁", "胃宿6度", "毕宿11度"),
        ("实沈", "毕宿11度", "井宿15度"),
        ("鹑首", "井宿15度", "柳宿8度"),
        ("鹑火", "柳宿8度", "张宿17度"),
        ("鹑尾", "张宿17度", "轸宿11度"),
        ("寿星", "轸宿11度", "氐宿4度"),
        ("大火", "氐宿4度", "尾宿9度"),
        ("析木", "尾宿9度", "斗宿11度")
    };

     
        private int currentYear = DateTime.Now.Year;

        private DateTime currentDateTime;


        public MainWindow()
        {
            InitializeComponent();

            defaultFont = new FontFamily("SimSun"); // 或者其他您喜欢的字体

            scaleTransform = new ScaleTransform(1, 1);
            translateTransform = new TranslateTransform(0, 0);
            transformGroup = new TransformGroup();
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(translateTransform);
            ChartCanvas.RenderTransform = transformGroup;

            // 初始化为当前日期和时间
            currentDateTime = DateTime.Now;

            // 初始化动画定时器
            InitializeAnimationTimer();

            currentDate = AstronomicalDate.FromDateTime(DateTime.Now);
            UpdateDateTimeInputs();



        ChartCanvas.MouseWheel += ChartCanvas_MouseWheel;
            ChartCanvas.MouseLeftButtonDown += ChartCanvas_MouseLeftButtonDown;
            ChartCanvas.MouseLeftButtonUp += ChartCanvas_MouseLeftButtonUp;
            ChartCanvas.MouseMove += ChartCanvas_MouseMove;


            YearInput.Text = currentYear.ToString();
            YearInput.TextChanged += YearInput_TextChanged;
            ChartCanvas.SizeChanged += ChartCanvas_SizeChanged;
          
            DrawChart();

            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (isDragging)
            {
                Point currentPosition = Mouse.GetPosition(this);
                Vector delta = currentPosition - lastMousePosition;

                translateTransform.X += delta.X;
                translateTransform.Y += delta.Y;

                lastMousePosition = currentPosition;
            }
        }

        private void DrawChart()
        {
            ChartCanvas.Children.Clear();

            // 计算缩放后的尺寸
            double scaledWidth = ChartCanvas.ActualWidth / scaleTransform.ScaleX;
            double scaledHeight = ChartCanvas.ActualHeight / scaleTransform.ScaleY;
            double diameter = Math.Min(scaledWidth, scaledHeight) - 70;
            double radius = diameter / 2;
            Point center = new Point(scaledWidth / 2, scaledHeight / 2);



            DrawCircle(center, radius, Brushes.LightGray, 1);
            DrawRadialLines(center, radius);
            DrawTwentyEightMansionsCircle(center, radius);
            DrawSolarTerms(center, radius);
            DrawTwentyEightMansions(center, radius);
            DrawCardinalDirections(center, radius);
            // DrawTexts(center, radius);
            DrawTwelveDivisions(center, radius); // 确保这行代码存在并且未被注释

            DrawCelestialBodies(center, radius);
            // ... 其他绘制方法 ...
        }

       

        private void UpdateDateTimeDisplay()
        {
           DateTimeDisplay.Text = currentDate.ToString();
        }

        private void UpdateDateTimeInputs()
        {
            YearInput.Text = currentDate.Year.ToString();
            MonthInput.Text = currentDate.Month.ToString("D2");
            DayInput.Text = currentDate.Day.ToString("D2");
            TimeInput.Text = $"{currentDate.Hour:D2}:{currentDate.Minute:D2}:{currentDate.Second:D2}";

            UpdateDateTimeDisplay();
            DrawChart();
        }


        private void InitializeAnimationTimer()
        {
            animationTimer = new DispatcherTimer();
            animationTimer.Interval = TimeSpan.FromMilliseconds(50); // 每50毫秒更新一次
            animationTimer.Tick += AnimationTimer_Tick;
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            double newValue = (TimeSlider.Value + 1) % 366;
            if (newValue == 0)
            {
                // 年份变化
                int newYear = currentDate.Year + (currentDate.Year < 0 ? -1 : 1);
                currentDate = new AstronomicalDate(newYear, 1, 1, 0, 0, 0);
            }
            else
            {
                currentDate.AddDays(1);
            }
            TimeSlider.Value = newValue;
        }

        private void ToggleAnimation()
        {
            if (isAnimating)
            {
                animationTimer.Stop();
                isAnimating = false;
            }
            else
            {
                animationTimer.Start();
                isAnimating = true;
            }
        }



        private void AnimationButton_Click(object sender, RoutedEventArgs e)
        {
            if (AnimationButton.IsChecked == true)
            {
                StartAnimation();
                AnimationButton.Content = "停止动画";
            }
            else
            {
                StopAnimation();
                AnimationButton.Content = "开始动画";
            }
        }

        private void StartAnimation()
        {
            // 开始动画的代码
            animationTimer.Start();
            isAnimating = true;
        }

        private void StopAnimation()
        {
            // 停止动画的代码
            animationTimer.Stop();
            isAnimating = false;
        }
        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(YearInput.Text, out int year) &&
                int.TryParse(MonthInput.Text, out int month) &&
                int.TryParse(DayInput.Text, out int day) &&
                TimeSpan.TryParse(TimeInput.Text, out TimeSpan time))
            {
                try
                {
                    // 直接使用输入的年份，包括负数
                    currentDate = new AstronomicalDate(year, month, day, time.Hours, time.Minutes, time.Seconds);
                    UpdateDateTimeInputs();
                    DrawChart();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"发生错误: {ex.Message}", "输入错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("请输入有效的日期和时间。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private void LimitPan()
        {
            Rect chartBounds = VisualTreeHelper.GetDescendantBounds(ChartCanvas);
            double scale = scaleTransform.ScaleX;

            double minX = this.ActualWidth - chartBounds.Width * scale;
            double minY = this.ActualHeight - chartBounds.Height * scale;

            translateTransform.X = Math.Min(0, Math.Max(minX, translateTransform.X));
            translateTransform.Y = Math.Min(0, Math.Max(minY, translateTransform.Y));
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
           // currentDate = AstronomicalDate.FromDateTime(DateTime.Now);
          //  UpdateDateTimeInputs();
           // DrawChart();

            scaleTransform.ScaleX = 1;
            scaleTransform.ScaleY = 1;
            translateTransform.X = 0;
            translateTransform.Y = 0;
            currentScale = 1;
            DrawChart();
        }

        private void AnimateReset()
        {
            var scaleAnimation = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(AnimationDurationMs),
                EasingFunction = new QuadraticEase()
            };

            var translateAnimationX = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(AnimationDurationMs),
                EasingFunction = new QuadraticEase()
            };

            var translateAnimationY = translateAnimationX.Clone();

            scaleAnimation.Completed += (s, e) =>
            {
                currentScale = 1.0;
                DrawChart();
            };

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation.Clone());
            translateTransform.BeginAnimation(TranslateTransform.XProperty, translateAnimationX);
            translateTransform.BeginAnimation(TranslateTransform.YProperty, translateAnimationY);
        }

        private void TimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int daysFromYearStart = (int)e.NewValue;

            // 创建当年的第一天
            var yearStart = new AstronomicalDate(currentDate.Year, 1, 1, 0, 0, 0);

            // 加上滑块值对应的天数
            yearStart.AddDays(daysFromYearStart);
            currentDate = yearStart;

            UpdateDateTimeInputs();
            DrawChart();
        }

        private void UpdateCurrentDateDisplay()
        {
            // 假设 AstronomicalDate 有一个 ToString 方法或类似的方法来格式化日期
            CurrentDateDisplay.Text = currentDate.ToString();
            // 如果没有合适的 ToString 方法，您可能需要手动格式化，例如：
            // CurrentDateDisplay.Text = $"{currentDate.Year:D4}-{currentDate.Month:D2}-{currentDate.Day:D2}";
        }
        private void ChartCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point mousePos = e.GetPosition(ChartCanvas);

            double zoomFactor = e.Delta > 0 ? ScaleFactor : 1 / ScaleFactor;
            double targetScale = currentScale * zoomFactor;
            targetScale = Math.Max(0.1, Math.Min(10, targetScale));

            Point mousePositionOnImage = new Point(
                (mousePos.X - translateTransform.X) / scaleTransform.ScaleX,
                (mousePos.Y - translateTransform.Y) / scaleTransform.ScaleY);

            scaleTransform.ScaleX = targetScale;
            scaleTransform.ScaleY = targetScale;

            translateTransform.X = mousePos.X - mousePositionOnImage.X * targetScale;
            translateTransform.Y = mousePos.Y - mousePositionOnImage.Y * targetScale;

            currentScale = targetScale;


            e.Handled = true;
        }

        private void YearUpButton_Click(object sender, RoutedEventArgs e)
        {
            currentDate.AddYears(1);
            UpdateDateTimeInputs();
            DrawChart();
        }

        private void YearDownButton_Click(object sender, RoutedEventArgs e)
        {
            currentDate.AddYears(-1);
            UpdateDateTimeInputs();
            DrawChart();
        }

        private void MonthUpButton_Click(object sender, RoutedEventArgs e)
        {
            currentDate.AddMonths(1);
            UpdateDateTimeInputs();
            DrawChart();
        }

        private void MonthDownButton_Click(object sender, RoutedEventArgs e)
        {
            currentDate.AddMonths(-1);
            UpdateDateTimeInputs();
            DrawChart();
        }

        private void DayUpButton_Click(object sender, RoutedEventArgs e)
        {
            currentDate.AddDays(1);
            UpdateDateTimeInputs();
            DrawChart();
        }

        private void DayDownButton_Click(object sender, RoutedEventArgs e)
        {
            currentDate.AddDays(-1);
            UpdateDateTimeInputs();
            DrawChart();
        }

        private void TimeUpButton_Click(object sender, RoutedEventArgs e)
        {
            currentDate.AddHours(1);
            UpdateDateTimeInputs();
            DrawChart();
        }

        private void TimeDownButton_Click(object sender, RoutedEventArgs e)
        {
            currentDate.AddHours(-1);
            UpdateDateTimeInputs();
            DrawChart();
        }

        private void CurrentTimeButton_Click(object sender, RoutedEventArgs e)
        {
            currentDate = AstronomicalDate.FromDateTime(DateTime.Now);
            UpdateDateTimeInputs();
            DrawChart();
        }
        private void ChartCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startDragPoint = e.GetPosition(this);
            lastMousePosition = startDragPoint;
            isDragging = true;
            ChartCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void ChartCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                ChartCanvas.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void ChartCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point currentPosition = e.GetPosition(this);
                Vector delta = currentPosition - lastMousePosition;

                translateTransform.X += delta.X;
                translateTransform.Y += delta.Y;

                lastMousePosition = currentPosition;
                e.Handled = true;
            }
        }
        private void AnimateScale(double fromScale, double toScale, Point center)
        {
            var scaleAnimation = new DoubleAnimation
            {
                From = fromScale,
                To = toScale,
                Duration = TimeSpan.FromMilliseconds(AnimationDurationMs),
                EasingFunction = new QuadraticEase()
            };

            var translateAnimation = new Vector3DAnimation
            {
                Duration = TimeSpan.FromMilliseconds(AnimationDurationMs),
                EasingFunction = new QuadraticEase()
            };

            scaleAnimation.Completed += (s, e) =>
            {
                currentScale = toScale;
                DrawChart();
            };

            // 计算新的偏移量以保持鼠标位置下的点不变
            Point startPoint = new Point(
                (center.X - translateTransform.X) / scaleTransform.ScaleX,
                (center.Y - translateTransform.Y) / scaleTransform.ScaleY);

            Point endPoint = new Point(
                (center.X - translateTransform.X) / toScale,
                (center.Y - translateTransform.Y) / toScale);

            Vector3D startVector = new Vector3D(translateTransform.X, translateTransform.Y, 0);
            Vector3D endVector = new Vector3D(
                translateTransform.X + (endPoint.X - startPoint.X) * toScale,
                translateTransform.Y + (endPoint.Y - startPoint.Y) * toScale,
                0);

            translateAnimation.From = startVector;
            translateAnimation.To = endVector;

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation.Clone());
            translateTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(endVector.X, translateAnimation.Duration));
            translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(endVector.Y, translateAnimation.Duration));
        }

        private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawChart();
        }


        private (string Name, double StartLongitude)[] CalculateInitialLongitudes()
        {
            var result = new (string Name, double StartLongitude)[twentyEightMansions.Length];
            double currentLongitude = REFERENCE_LONGITUDE;
            int niuIndex = Array.FindIndex(twentyEightMansions, m => m.Name == "牛");

            for (int i = 0; i < twentyEightMansions.Length; i++)
            {
                int adjustedIndex = (i + niuIndex) % twentyEightMansions.Length;
                result[adjustedIndex] = (twentyEightMansions[adjustedIndex].Name, currentLongitude);
                currentLongitude = (currentLongitude + twentyEightMansions[adjustedIndex].Degrees * ECLIPTIC_DEGREES / TOTAL_DEGREES) % ECLIPTIC_DEGREES;
            }

            return result;
        }

        private (string Name, double StartLongitude, double EndLongitude)[] CalculateAccurateLongitudes(int year)
        {
            var result = new (string Name, double StartLongitude, double EndLongitude)[twentyEightMansions.Length];
            double totalDegrees = twentyEightMansions.Sum(m => m.Degrees);
            double currentDegree = 0;
            double precessionAdjustment = CalculatePrecession(year);

            // 找到牛宿的索引
            int niuIndex = Array.FindIndex(twentyEightMansions, m => m.Name == "牛");

            for (int i = 0; i < twentyEightMansions.Length; i++)
            {
                int adjustedIndex = (i + niuIndex) % twentyEightMansions.Length;
                var mansion = twentyEightMansions[adjustedIndex];

                double startLongitude = (REFERENCE_LONGITUDE + currentDegree * 360 / totalDegrees + precessionAdjustment) % 360;
                currentDegree += mansion.Degrees;
                double endLongitude = (REFERENCE_LONGITUDE + currentDegree * 360 / totalDegrees + precessionAdjustment) % 360;

                result[adjustedIndex] = (mansion.Name, startLongitude, endLongitude);
            }

            return result;
        }

        private void YearInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(YearInput.Text, out int year))
            {
                currentYear = year;
               // UpdateDateTimeInputs();
                DrawChart();
            }
          
        }



       


        private void DrawCircle(Point center, double radius, Brush stroke, double thickness)
        {
            if (radius <= 0) return;

            Ellipse circle = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = stroke,
                StrokeThickness = thickness
            };
            Canvas.SetLeft(circle, center.X - radius);
            Canvas.SetTop(circle, center.Y - radius);
            ChartCanvas.Children.Add(circle);
        }
        private void DrawCircles(Point center, double radius)
        {
            double[] radiusFactors = { 1, 0.95, 0.9, 0.85, 0.8, 0.75, 0.7, 0.65, 0.6, 0.55, 0.5, 0.45 };
            foreach (double factor in radiusFactors)
            {
                DrawCircle(center, radius * factor, Brushes.LightGray, 0.5);
            }

            // 添加28宿专用圆
            DrawCircle(center, radius * 0.82, Brushes.DarkGreen, 1.5);
        }

        private void DrawTwentyEightMansionsCircle(Point center, double radius)
        {
            double mansionsRadius = radius * 0.82;
            DrawCircle(center, mansionsRadius, Brushes.DarkGreen, 1.5);
        }



        private void DrawSolarTerms(Point center, double radius)
        {
           

            foreach (var term in solarTerms)
            {
                double adjustedAngle = (TOTAL_DEGREES - term.Longitude) * 360 / TOTAL_DEGREES;
                double radians = adjustedAngle * Math.PI / 180;
                Point textPoint = GetPointOnCircle(center, radius * 0.87, radians);
                DrawRotatedText(radius, term.Name, textPoint, radians, 8, Brushes.Red);

                Point longitudePoint = GetPointOnCircle(center, radius * 0.93, radians);
                DrawRotatedText(radius, $"{term.Longitude:F2}°", longitudePoint, radians, 6, Brushes.Blue);
                
                double solarTermRadius = radius * 0.918; // 调整这个值来放置节气标记
                double angle = (360 - term.Longitude) * Math.PI / 180; // 转换为弧度
                Point termPoint = GetPointOnCircle(center, solarTermRadius, angle);
               
                DrawDot(termPoint, 2.5, Brushes.Red);
            }


          
        }

        private void DrawDot(Point center, double radius, Brush fill)
        {
            Ellipse dot = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = fill
            };
            Canvas.SetLeft(dot, center.X - radius);
            Canvas.SetTop(dot, center.Y - radius);
            ChartCanvas.Children.Add(dot);
        }

        private void DrawRotatedText(double radius, string text, Point center, double angle, double fontSize, Brush color, FontFamily fontFamily = null)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                Foreground = color,
                TextAlignment = TextAlignment.Center,
                FontFamily = fontFamily ?? defaultFont
            };

            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double textWidth = textBlock.DesiredSize.Width;
            double textHeight = textBlock.DesiredSize.Height;

            // 角度补偿
            double angleCompensation = Math.Atan2(textHeight / 2, radius);
            double compensatedAngle = angle;
            if (angle > Math.PI / 2 && angle < 3 * Math.PI / 2)
            {
                compensatedAngle += angleCompensation;
            }
            else
            {
                compensatedAngle -= angleCompensation;
            }

            // 计算文字位置
            Point textPoint = new Point(
                center.X + radius * 0.02 * Math.Cos(compensatedAngle),
                center.Y + radius * 0.02 * Math.Sin(compensatedAngle)
            );

            // 创建一个容器来持有文本
            Canvas textCanvas = new Canvas
            {
                Width = textWidth,
                Height = textHeight
            };
            textCanvas.Children.Add(textBlock);

            // 设置文本在容器中的位置
            Canvas.SetLeft(textBlock, -textWidth / 2);
            Canvas.SetTop(textBlock, -textHeight / 2);

            // 创建旋转变换
            double rotationAngle = compensatedAngle * 180 / Math.PI - 90;
            RotateTransform rotateTransform = new RotateTransform(rotationAngle);
            textCanvas.RenderTransform = rotateTransform;

            // 设置容器的位置
            Canvas.SetLeft(textCanvas, textPoint.X);
            Canvas.SetTop(textCanvas, textPoint.Y);

            ChartCanvas.Children.Add(textCanvas);
        }

        private void DrawRadialLines(Point center, double radius)
        {
            for (int i = 0; i < DIVISIONS; i++)
            {
                // 反转角度以确保正确的方向
                double angle = (360 - i * 360.0 / DIVISIONS) % 360;
                double radians = angle * Math.PI / 180;
                Point end = GetPointOnCircle(center, radius, radians);
                Line line = new Line
                {
                    X1 = center.X,
                    Y1 = center.Y,
                    X2 = end.X,
                    Y2 = end.Y,
                    Stroke = i % 3 == 0 ? Brushes.Blue : Brushes.LightBlue,
                    StrokeThickness = i % 3 == 0 ? 1 : 0.5
                };
                ChartCanvas.Children.Add(line);
            }
        }

        private Point GetPointOnCircle(Point center, double radius, double angle)
        {
            // 确保半径为正值
            radius = Math.Max(0, radius);

            double x = center.X + radius * Math.Cos(angle);
            double y = center.Y + radius * Math.Sin(angle);
            return new Point(x, y);
        }




        private void DrawTwentyEightMansions(Point center, double radius)
        {
            double mansionsRadius = radius * 0.82;
            double outerRadius = radius * 0.92;
            double textRadius = radius * 0.72; // 调整文本半径
            var accurateMansions = CalculateAccurateLongitudes(currentYear);

            // 初始化 mansionsWithLongitudes
            mansionsWithLongitudes = accurateMansions.Select(m => (m.Name, m.StartLongitude, m.EndLongitude)).ToArray();
            Debug.WriteLine($"Initialized mansionsWithLongitudes with {mansionsWithLongitudes.Length} mansions");

            // 绘制同心圆
            DrawCircle(center, mansionsRadius, Brushes.LightGray, 1);
            DrawCircle(center, outerRadius, Brushes.LightGray, 1);
            DrawCircle(center, textRadius, Brushes.LightGray, 0.5); // 添加文本参考圆（可选）

            foreach (var mansion in accurateMansions)
            {
                // 绘制宿的起始和结束经度线（绿色虚线）
                DrawLongitudeLine(center, radius * 1.1, mansion.StartLongitude, Brushes.Green, true);
                DrawLongitudeLine(center, radius * 1.1, mansion.EndLongitude, Brushes.Green, true);

                // 计算宿的中心点位置
                double middleLongitude = (mansion.StartLongitude + mansion.EndLongitude) / 2;
                if (mansion.EndLongitude < mansion.StartLongitude)
                    middleLongitude = (middleLongitude + 180) % 360;
                double angle = 360 - middleLongitude;
                double radians = angle * Math.PI / 180;

                // 绘制宿的位置标记
                Point mansionPoint = GetPointOnCircle(center, mansionsRadius, radians);
                DrawMansionMarker(mansionPoint, 2.5, Brushes.Green);

                // 绘制宿的名称
                Point textPoint = GetPointOnCircle(center, outerRadius + 15, radians);
                DrawRotatedText(radius,mansion.Name, textPoint, radians, 10, Brushes.Green);

                // 绘制宿的度数和经度范围
                Point degreePoint = GetPointOnCircle(center, textRadius, radians);
                double mansionDegrees = (mansion.EndLongitude - mansion.StartLongitude + 360) % 360;
                string text = $"{mansionDegrees:F1}° \n({mansion.StartLongitude:F1}°-{mansion.EndLongitude:F1}°)";
                DrawRotatedText(radius,text, degreePoint, radians, 6, Brushes.DarkGreen);

                Debug.WriteLine($"Drew mansion: {mansion.Name}, Start: {mansion.StartLongitude:F1}°, End: {mansion.EndLongitude:F1}°");
            }
        }

        private void DrawLongitudeLine(Point center, double radius, double longitude, Brush color, bool isDashed = false)
        {
            if (radius <= 0)
            {
                // 如果半径小于或等于0，跳过绘制
                return;
            }

            double angle = (360 - longitude) * Math.PI / 180;
            Point end = GetPointOnCircle(center, radius, angle);

            Line line = new Line
            {
                X1 = center.X,
                Y1 = center.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = color,
                StrokeThickness = 1
            };

            if (isDashed)
            {
                line.StrokeDashArray = new DoubleCollection { 4, 4 };
            }

            ChartCanvas.Children.Add(line);
        }



        private void DrawTwelveDivisions(Point center, double radius)
        {
            Debug.WriteLine("Entering DrawTwelveDivisions method");

            if (twelveDivisions == null)
            {
                Debug.WriteLine("Error: twelveDivisions is null");
                return;
            }

            Debug.WriteLine($"Number of divisions: {twelveDivisions.Length}");

            double divisionRadius = radius * 0.98;
            double textRadius = radius * 1.05;

            for (int i = 0; i < twelveDivisions.Length; i++)
            {
                var division = twelveDivisions[i];
                Debug.WriteLine($"\nProcessing division {i}: {division.Name}");
                Debug.WriteLine($"Start description: {division.Start}, End description: {division.End}");

                try
                {
                    var (startAngle, startDebug) = GetAngleFromMansion(division.Start);
                    var (endAngle, endDebug) = GetAngleFromMansion(division.End);

                    Debug.WriteLine($"{division.Name}: Start {startDebug}, End {endDebug}");

                    // 处理跨越0度的情况
                    if (endAngle < startAngle)
                    {
                        endAngle += 360;
                    }

                    Debug.WriteLine($"Final angles - Start: {startAngle:F2}°, End: {endAngle:F2}°");

                    // 绘制天区范围
                    DrawArc(center, divisionRadius, divisionRadius + 10, startAngle, endAngle, new SolidColorBrush(Color.FromArgb(50, 255, 165, 0)));

                    // 绘制起始和结束线
                    DrawLongitudeLine(center, divisionRadius, startAngle, Brushes.Orange, 2, true);
                    DrawLongitudeLine(center, divisionRadius, endAngle % 360, Brushes.Orange, 2, true);

                    // 计算标签角度
                    double middleAngle = (startAngle + endAngle) / 2;
                    Point textPoint = GetPointOnCircle(center, textRadius, (360-middleAngle) * Math.PI / 180);

                    string text = $"{division.Name} ({startAngle:F1}°-{endAngle:F1}°)";
                    DrawRotatedText(radius,text, textPoint, (360-middleAngle) * Math.PI / 180, 10, Brushes.Orange);

                    Debug.WriteLine($"Drew division: {division.Name}, Start: {startAngle:F1}°, End: {endAngle:F1}°");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error drawing division {division.Name}: {ex.Message}");
                }
            }

            Debug.WriteLine("Exiting DrawTwelveDivisions method");
        }


        private void DrawLongitudeLine(Point center, double radius, double longitude, Brush color, double thickness = 1, bool isDashed = false)
        {
            double angle = (360 - longitude) * Math.PI / 180;
            Point end = GetPointOnCircle(center, radius, angle);

            Line line = new Line
            {
                X1 = center.X,
                Y1 = center.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = color,
                StrokeThickness = thickness
            };

            if (isDashed)
            {
                line.StrokeDashArray = new DoubleCollection { 4, 4 };
            }

            ChartCanvas.Children.Add(line);
        }

        private (double angle, string DebugInfo) GetAngleFromMansion(string mansionDescription)
        {
            Debug.WriteLine($"GetAngleFromMansion called with: {mansionDescription}");

            if (string.IsNullOrEmpty(mansionDescription))
            {
                return (0, $"Error: mansionDescription is null or empty");
            }

            try
            {
                var (mansionName, offset) = ExtractMansionInfo(mansionDescription);

                var mansionInfo = mansionsWithLongitudes.FirstOrDefault(m => m.Name == mansionName);
                if (mansionInfo == default)
                {
                    Debug.WriteLine($"Mansion not found: {mansionName}");
                    return (0, $"Mansion not found: {mansionName}");
                }

                // 使用宿的起始经度加上偏移量
                double result = (mansionInfo.StartLongitude + offset) % 360;

                Debug.WriteLine($"Calculated angle for {mansionName}: {result:F2}°");
                return (result, $"{mansionName} {offset}°: {result:F2}°");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetAngleFromMansion: {ex.Message}");
                return (0, $"Error: {ex.Message}");
            }
        }


       private (string mansionName, double offset) ExtractMansionInfo(string description)
{
    string[] parts = description.Split('宿');
    if (parts.Length != 2)
    {
        throw new ArgumentException($"Invalid mansion description format: {description}");
    }

    string mansionName = parts[0].Trim(); // 移除 "宿" 字
    string degreePart = parts[1].Trim().TrimEnd('度');

    if (!double.TryParse(degreePart, out double offset))
    {
        throw new ArgumentException($"Failed to parse degrees from: {degreePart}");
    }

    Debug.WriteLine($"Extracted mansion info: {mansionName}, offset: {offset}");
    return (mansionName, offset);
}


        private void CalculateMansionsWithLongitudes()
        {
            try
            {
                if (twentyEightMansions == null)
                {
                    Debug.WriteLine("Error: twentyEightMansions is null");
                    return;
                }
                if (twentyEightMansions.Length == 0)
                {
                    Debug.WriteLine("Error: twentyEightMansions is empty");
                    return;
                }

                mansionsWithLongitudes = new (string Name, double StartLongitude, double EndLongitude)[twentyEightMansions.Length];
                double currentLongitude = 0;

                Debug.WriteLine("Calculating mansions with longitudes:");
                for (int i = 0; i < twentyEightMansions.Length; i++)
                {
                    if (twentyEightMansions[i].Name == null)
                    {
                        Debug.WriteLine($"Error: Mansion name is null at index {i}");
                        continue;
                    }

                    double startLongitude = currentLongitude;
                    double mansionDegrees = twentyEightMansions[i].Degrees * ECLIPTIC_DEGREES / TOTAL_DEGREES;
                    double endLongitude = (currentLongitude + mansionDegrees) % 360;
                    mansionsWithLongitudes[i] = (twentyEightMansions[i].Name, startLongitude, endLongitude);
                    currentLongitude = endLongitude;

                    Debug.WriteLine($" {mansionsWithLongitudes[i].Name}: Start {mansionsWithLongitudes[i].StartLongitude:F2}°, End {mansionsWithLongitudes[i].EndLongitude:F2}°");
                }

                Debug.WriteLine($"Calculated {mansionsWithLongitudes.Length} mansions with longitudes");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in CalculateMansionsWithLongitudes: {ex.Message}");
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private double GetMansionStartLongitude(int mansionIndex)
        {
            double totalDegrees = 0;
            for (int i = 0; i < mansionIndex; i++)
            {
                totalDegrees += twentyEightMansions[i].Degrees;
            }
            return (totalDegrees * ECLIPTIC_DEGREES / TOTAL_DEGREES) % 360;
        }
        private void DrawEccentricCircle(Point center, double majorRadius, double minorRadius, Brush stroke, double thickness)
        {
            EllipseGeometry ellipse = new EllipseGeometry(center, majorRadius, minorRadius);
            Path path = new Path
            {
                Stroke = stroke,
                StrokeThickness = thickness,
                Data = ellipse
            };
            ChartCanvas.Children.Add(path);
        }

        private void DrawLongitudeLine(Point center, double radius, double longitude, Brush color)
        {
            double angle = (360 - longitude) * Math.PI / 180;
            Point end = GetPointOnCircle(center, radius, angle);

            Line line = new Line
            {
                X1 = center.X,
                Y1 = center.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = color,
                StrokeThickness = 1
            };
            ChartCanvas.Children.Add(line);
        }

        private void DrawMansionMarker(Point center, double radius, Brush fill)
        {
            Ellipse marker = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = fill
            };
            Canvas.SetLeft(marker, center.X - radius);
            Canvas.SetTop(marker, center.Y - radius);
            ChartCanvas.Children.Add(marker);
        }

        private double CalculatePrecession(int year)
        {
            // return (year - REFERENCE_YEAR) * PRECESSION_RATE;

         
                int adjustedYear = year;
                int adjustedReferenceYear = REFERENCE_YEAR;

                // 调整公元前年份
                if (year <= 0) adjustedYear++;
                if (REFERENCE_YEAR <= 0) adjustedReferenceYear++;

                // 计算年份差
                int yearDifference = adjustedYear - adjustedReferenceYear;

                return yearDifference * PRECESSION_RATE;
            
        }



        private void DrawArc(Point center, double innerRadius, double outerRadius, double startAngle, double endAngle, Brush fill)
        {
            startAngle = startAngle * Math.PI / 180;
            endAngle = endAngle * Math.PI / 180;

            Point innerStart = GetPointOnCircle(center, innerRadius, startAngle);
            Point outerStart = GetPointOnCircle(center, outerRadius, startAngle);
            Point innerEnd = GetPointOnCircle(center, innerRadius, endAngle);
            Point outerEnd = GetPointOnCircle(center, outerRadius, endAngle);

            bool isLargeArc = Math.Abs(endAngle - startAngle) > Math.PI;

            var figure = new PathFigure
            {
                StartPoint = innerStart,
                Segments = new PathSegmentCollection
            {
                new LineSegment(outerStart, true),
                new ArcSegment(outerEnd, new Size(outerRadius, outerRadius), 0, isLargeArc, SweepDirection.Clockwise, true),
                new LineSegment(innerEnd, true),
                new ArcSegment(innerStart, new Size(innerRadius, innerRadius), 0, isLargeArc, SweepDirection.Counterclockwise, true)
            }
            };

            var pathGeometry = new PathGeometry(new[] { figure });
            var path = new Path { Fill = fill, Data = pathGeometry };
            ChartCanvas.Children.Add(path);
        }

        private void DrawCardinalDirections(Point center, double radius)
        {
            string[] directions = { "西", "北", "东", "南" };
            for (int i = 0; i < 4; i++)
            {
                double angle = i * 90;
                double radians = angle * Math.PI / 180;
                Point textPoint = GetPointOnCircle(center, radius *1.1, radians);
                DrawRotatedText(radius, directions[i], textPoint, radians, 14, Brushes.Red);
            }
        }


        private void DrawTexts(Point center, double radius)
        {
            string[] zodiacSigns = { "白羊", "金牛", "双子", "巨蟹", "狮子", "处女", "天秤", "天蝎", "射手", "摩羯", "水瓶", "双鱼" };
          //  DrawCircularText(center, radius * 0.97, zodiacSigns, 10, Brushes.Purple);

            string[] constellations = { "白羊", "金牛", "双子", "巨蟹", "狮子", "处女", "天秤", "天蝎", "人马", "摩羯", "宝瓶", "双鱼" };
            DrawCircularText(center, radius *1.05, constellations, 8, Brushes.Green);
        }

        private void DrawCenterSymbols(Point center, double radius)
        {
            string[] symbols = { "北极", "天球", "地球" };
            for (int i = 0; i < symbols.Length; i++)
            {
                DrawRotatedText(radius, symbols[i], new Point(center.X, center.Y - radius * 0.2 * i), 0, 12, Brushes.Black);
            }
        }





        



        private void DrawEclipticAndCelestialEquator(Point center, double radius)
        {
            // 绘制黄道
            DrawCircle(center, radius * 0.8, Brushes.Yellow, 1);

            // 绘制天赤道
            EllipseGeometry celestialEquator = new EllipseGeometry(center, radius * 0.8, radius * 0.8 * Math.Cos(ECLIPTIC_TILT * Math.PI / 180));
            Path equatorPath = new Path
            {
                Stroke = Brushes.Blue,
                StrokeThickness = 1,
                Data = celestialEquator
            };
            ChartCanvas.Children.Add(equatorPath);
        }



        private void DrawScales(Point center, double radius)
        {
            DrawScale(center, radius, 360, 5, Brushes.Red, 0.5);
            DrawScale(center, radius * 0.95, 240, 3, Brushes.Blue, 0.5);
            DrawScale(center, radius * 0.9, 120, 2, Brushes.Yellow, 0.5);
        }

        private void DrawScale(Point center, double radius, int divisions, int subDivisions, Brush color, double thickness)
        {
            for (int i = 0; i < divisions * subDivisions; i++)
            {
                double angle = i * Math.PI * 2 / (divisions * subDivisions);
                Point outer = GetPointOnCircle(center, radius, angle);
                Point inner = GetPointOnCircle(center, radius - (i % subDivisions == 0 ? 5 : 3), angle);
                Line line = new Line
                {
                    X1 = inner.X,
                    Y1 = inner.Y,
                    X2 = outer.X,
                    Y2 = outer.Y,
                    Stroke = color,
                    StrokeThickness = thickness
                };
                ChartCanvas.Children.Add(line);
            }
        }

       

        private void DrawCircularText(Point center, double radius, string[] texts, double fontSize, Brush color)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                double angle = i * Math.PI * 2 / texts.Length - Math.PI / 2;
                Point textPoint = GetPointOnCircle(center, radius, angle);
                DrawRotatedText(radius, texts[i], textPoint, angle, fontSize, color);
            }
        }

      

        private void DrawTriangleMarkers(Point center, double radius)
        {
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                Point start = GetPointOnCircle(center, radius * 0.6, angle);
                Point end = GetPointOnCircle(center, radius * 0.7, angle);
                Line line = new Line
                {
                    X1 = start.X,
                    Y1 = start.Y,
                    X2 = end.X,
                    Y2 = end.Y,
                    Stroke = Brushes.Yellow,
                    StrokeThickness = 3
                };
                ChartCanvas.Children.Add(line);
            }
        }

        private void DrawCelestialBodies(Point center, double radius)
        {
            // 使用 currentDate.ToDateTime() 获取用于计算的 DateTime 对象
            DateTime dateTimeForCalculation = currentDate.ToDateTime();

            double sunPosition = CelestialBodyPositionCalculator.CalculateSunPosition(currentDate);
            double moonPosition = CelestialBodyPositionCalculator.CalculateMoonPosition(currentDate);
            double marsPosition = CelestialBodyPositionCalculator.CalculateMarsPosition(currentDate);
          
            DrawCelestialBody(center, radius, sunPosition, Brushes.Red, "太阳", 15);
            DrawCelestialBody(center, radius, moonPosition, Brushes.Silver, "月亮", 10);
            DrawCelestialBody(center, radius, marsPosition, Brushes.DarkViolet, "火星", 8);

          
        }


        private void DrawCelestialBody(Point center, double radius, double position, Brush color, string name, double size)
        {
            double angle = position * Math.PI / 180;
            Point bodyPoint = GetPointOnCircle(center, radius * 0.8, angle);

            Ellipse body = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = color
            };
            Canvas.SetLeft(body, bodyPoint.X - size / 2);
            Canvas.SetTop(body, bodyPoint.Y - size / 2);
            ChartCanvas.Children.Add(body);

            // 调整文本位置，使其位于天体外侧
            Point textPoint = GetPointOnCircle(center, radius * 0.70, angle);
            DrawRotatedText(radius, name, textPoint, angle, 10, color);
        }

        private void DrawCelestialBody(Point center, double radius, double position, Brush color, string name)
        {
            // 将位置转换为弧度，注意这里不需要再做 360 - position，因为我们在计算时已经调整了
            double angle = position * Math.PI / 180;
            Point bodyPoint = GetPointOnCircle(center, radius * 0.8, angle);

            Ellipse body = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = color
            };
            Canvas.SetLeft(body, bodyPoint.X - 5);
            Canvas.SetTop(body, bodyPoint.Y - 5);
            ChartCanvas.Children.Add(body);

            // 调整文本位置，使其位于天体外侧
            Point textPoint = GetPointOnCircle(center, radius * 0.85, angle);
            DrawRotatedText(radius, name, textPoint, angle, 10, color);
        }




    }


    //====================================

    public class CelestialBodyPositionCalculator
    {
        private static readonly DateTime J2000 = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        public static double CalculateSunPosition(AstronomicalDate date)
        {
            double jd = date.JulianDay;
            double T = (jd - 2451545.0) / 36525.0;
            double L0 = 280.46646 + 36000.76983 * T + 0.0003032 * T * T;

            // 归一化到 0-360 度
            L0 = (L0 % 360 + 360) % 360;

            // 调整角度以匹配图表
            // 0 度对应春分（图上的右边）
            double adjustedAngle = (360 - L0) % 360;

            return adjustedAngle;
        }

        public static double CalculateMoonPosition(AstronomicalDate date)
        {
            double jd = date.JulianDay;
            double T = (jd - 2451545.0) / 36525.0;
            double L0 = 218.3164477 + 481267.88123421 * T - 0.0015786 * T * T;
            L0 = (L0 % 360 + 360) % 360;
            return (360 - L0) % 360;
        }

        public static double CalculateMarsPosition(AstronomicalDate date)
        {
            double jd = date.JulianDay;
            double T = (jd - 2451545.0) / 36525.0;
            double L0 = 355.45332 + 68905103.78 * T;
            L0 = (L0 % 360 + 360) % 360;
            return (360 - L0) % 360;
        }

        private static double GetJulianDaysSinceJ2000(DateTime date)
        {
            return (date.ToUniversalTime() - J2000).TotalDays;
        }

        private static double NormalizeAngle(double angle)
        {
            return (angle % 360 + 360) % 360;
        }
    }


    //==================================

   

    public class AstronomicalDate
    {
        private double julianDay;

        public AstronomicalDate(int year, int month, int day, int hour, int minute, int second)
        {
            // 使用天文年计算儒略日，不需要特殊处理负数年份
            // 直接存储输入的值，不进行转换
            Year = year;
            Month = month;
            Day = day;
            Hour = hour;
            Minute = minute;
            Second = second;
            julianDay = CalculateJulianDay(year, month, day, hour, minute, second);
        }

        public static AstronomicalDate FromDateTime(DateTime dateTime)
        {
            return new AstronomicalDate(dateTime.Year, dateTime.Month, dateTime.Day,
                                        dateTime.Hour, dateTime.Minute, dateTime.Second);
        }

        public int Year { get; private set; }
        public int Month { get; private set; }
        public int Day { get; private set; }
        public int Hour { get; private set; }
        public int Minute { get; private set; }
        public int Second { get; private set; }
        public double JulianDay => julianDay; // 添加这个属性以便访问儒略日

        private DateTime GetCalendarDate()
        {
            // 转换儒略日到日历日期
            double jd = julianDay + 0.5;
            int z = (int)jd;
            double f = jd - z;
            int a = z;
            if (z >= 2299161)
            {
                int alpha = (int)((z - 1867216.25) / 36524.25);
                a = z + 1 + alpha - alpha / 4;
            }
            int b = a + 1524;
            int c = (int)((b - 122.1) / 365.25);
            int d = (int)(365.25 * c);
            int e = (int)((b - d) / 30.6001);

            int day = b - d - (int)(30.6001 * e) + (int)f;
            int month = e < 14 ? e - 1 : e - 13;
            int year = month > 2 ? c - 4716 : c - 4715;

            // 处理时分秒
            double fractionalDay = f;
            int hour = (int)(fractionalDay * 24);
            int minute = (int)((fractionalDay * 24 - hour) * 60);
            int second = (int)(((fractionalDay * 24 - hour) * 60 - minute) * 60);

            // 不需要特殊处理公元前年份，直接返回计算结果
            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
        }

        private static double CalculateJulianDay(int year, int month, int day, int hour, int minute, int second)
        {
            // 处理公元前年份
            if (year < 0) year++;

            double y = year;
            double m = month;
            if (m <= 2)
            {
                y -= 1;
                m += 12;
            }

            double a = Math.Floor(y / 100.0);
            double b = 2 - a + Math.Floor(a / 4.0);

            return Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + day + b - 1524.5
                   + (hour + minute / 60.0 + second / 3600.0) / 24.0;
        }

        public override string ToString()
        {
            string yearStr = Year <= 0 ? $"{-Year + 1} BCE" : $"{Year} CE";
            return $"{yearStr}-{Month:D2}-{Day:D2} {Hour:D2}:{Minute:D2}:{Second:D2}";
        }



        public DateTime ToDateTime()
        {
            if (Year > 0 && Year < 10000)
            {
                return new DateTime(Year, Month, Day, Hour, Minute, Second, DateTimeKind.Utc);
            }
            else
            {
                // 对于超出 DateTime 范围的年份，返回一个表示遥远过去或未来的日期
                return Year < 0 ? DateTime.MinValue : DateTime.MaxValue;
            }
        }
        public void AddYears(int years)
        {
            Year += years;
            UpdateJulianDay();
        }

        public void AddMonths(int months)
        {
            int yearsToAdd = months / 12;
            int newMonth = Month + months % 12;
            if (newMonth > 12)
            {
                yearsToAdd++;
                newMonth -= 12;
            }
            else if (newMonth < 1)
            {
                yearsToAdd--;
                newMonth += 12;
            }
            Year += yearsToAdd;
            Month = newMonth;
            UpdateJulianDay();
        }

        public void AddDays(int days)
        {
            julianDay += days;
            UpdateDateFromJulianDay();
        }

        public void AddHours(int hours)
        {
            julianDay += hours / 24.0;
            UpdateDateFromJulianDay();
        }

        private void UpdateJulianDay()
        {
            julianDay = CalculateJulianDay(Year, Month, Day, Hour, Minute, Second);
        }

        private void UpdateDateFromJulianDay()
        {
            // 从儒略日更新年、月、日
            double j = julianDay + 0.5;
            int z = (int)j;
            double f = j - z;

            int a;
            if (z >= 2299161)
            {
                a = (int)((z - 1867216.25) / 36524.25);
                a = z + 1 + a - (a / 4);
            }
            else
            {
                a = z;
            }

            int b = a + 1524;
            int c = (int)((b - 122.1) / 365.25);
            int d = (int)(365.25 * c);
            int e = (int)((b - d) / 30.6001);

            Day = b - d - (int)(30.6001 * e);
            Month = e < 14 ? e - 1 : e - 13;
            Year = Month > 2 ? c - 4716 : c - 4715;

            // 处理公元前年份
            if (Year <= 0) Year--;

            // 更新时分秒
            double hours = f * 24;
            Hour = (int)hours;
            double minutes = (hours - Hour) * 60;
            Minute = (int)minutes;
            Second = (int)((minutes - Minute) * 60);
        }


        private static double DateToJulianDay(DateTime date)
        {
            return date.ToOADate() + 2415018.5;
        }

        private static DateTime JulianDayToDate(double julianDay)
        {
            return DateTime.FromOADate(julianDay - 2415018.5);
        }
    }


}