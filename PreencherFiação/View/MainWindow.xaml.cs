using System;
using System.Windows;
using Autodesk.Revit.UI;
using PreencherFiacao;

namespace PreencherFiacao.View
{
    public partial class MainWindow : Window
    {
        private UIDocument _uiDocument;
        private static MainWindow _mainWindowInstance;
        private static CircuitSelectionWindow _circuitSelectionWindow;
        private static PreencherFiacaoWindow _preencherFiacaoWindow;
        public PreencherFiacaoWindow _preecherFiacaoWindow;

        public MainWindow(UIDocument uiDocument)
        {
            InitializeComponent();
            _uiDocument = uiDocument;
        }

        public static void ShowMainWindow(UIDocument uiDocument)
        {
            if (_mainWindowInstance == null || !_mainWindowInstance.IsVisible)
            {
                _mainWindowInstance = new MainWindow(uiDocument);
                _mainWindowInstance.Closed += (s, args) => _mainWindowInstance = null;
                _mainWindowInstance.Show();
            }
            else
            {
                _mainWindowInstance.Activate();
            }
        }

        private void CalculateRouteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_circuitSelectionWindow == null || !_circuitSelectionWindow.IsVisible)
                {
                    // Minimiza a MainWindow antes de abrir a CircuitSelectionWindow
                    this.WindowState = WindowState.Minimized;

                    _circuitSelectionWindow = new CircuitSelectionWindow(_uiDocument);
                    _circuitSelectionWindow.Closed += (s, args) =>
                    {
                        _circuitSelectionWindow = null;

                        // Restaura a MainWindow quando a CircuitSelectionWindow for fechada
                        this.WindowState = WindowState.Normal;
                        this.Activate();
                    };

                    _circuitSelectionWindow.Show();
                }
                else
                {
                    _circuitSelectionWindow.Activate();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir a janela de seleção de circuitos: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CriarConduites_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Função de criação de conduítes ainda não implementada.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao criar conduítes: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreencherParametrosFiacao_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_preencherFiacaoWindow == null || !_preencherFiacaoWindow.IsVisible)
                {
                    // Minimiza a MainWindow antes de abrir a PreencherFiacaoWindow
                    this.WindowState = WindowState.Minimized;

                    _preencherFiacaoWindow = new PreencherFiacaoWindow(_uiDocument);

                    // Garante que a janela está restaurada corretamente
                    _preencherFiacaoWindow.WindowState = WindowState.Normal;
                    _preencherFiacaoWindow.ShowInTaskbar = true;
                    _preencherFiacaoWindow.Topmost = true;
                    _preencherFiacaoWindow.Activate();

                    _preencherFiacaoWindow.Closed += (s, args) =>
                    {
                        _preencherFiacaoWindow = null;

                        // Restaura a MainWindow quando a PreencherFiacaoWindow for fechada
                        this.WindowState = WindowState.Normal;
                        this.Activate();
                    };

                    _preencherFiacaoWindow.Show();
                }
                else
                {
                    _preencherFiacaoWindow.WindowState = WindowState.Normal;
                    _preencherFiacaoWindow.Activate();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir a janela de preenchimento de fiação: {ex.Message}",
                                "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
