using System;
using System.Linq;
using System.Windows;
using System.Collections.Generic;
using System.ComponentModel;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using PreencherFiacao;
using PreencherFiacao.Model;

namespace PreencherFiacao.View
{
    public partial class CircuitSelectionWindow : Window, INotifyPropertyChanged
    {
        private static CircuitSelectionWindow _instanciaAtiva; // Controle de instância única
        private UIDocument _uiDocument;
        private PreencherFiacao.Model.DefinirRotaCircuito _definirRotaCircuito;
        private PersonalizarRota _personalizarRotaLogic;
        private bool _isConfirmarEnabled = false;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Método para garantir que apenas uma instância da janela esteja ativa.
        /// </summary>
        public static void MostrarJanela(UIDocument uiDocument)
        {
            if (_instanciaAtiva == null || !_instanciaAtiva.IsLoaded)
            {
                _instanciaAtiva = new CircuitSelectionWindow(uiDocument);
                _instanciaAtiva.Show();
            }
            else
            {
                _instanciaAtiva.Activate();
            }
        }

        public CircuitSelectionWindow(UIDocument uiDocument)
        {
            InitializeComponent();
            DataContext = this; // Configuração do DataContext para bindings
            _uiDocument = uiDocument;
            _definirRotaCircuito = new PreencherFiacao.Model.DefinirRotaCircuito();
            _personalizarRotaLogic = new PersonalizarRota(_uiDocument);
            CarregarCircuitos();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _instanciaAtiva = null; // Libera a referência ao fechar a janela
        }

        public bool IsConfirmarEnabled
        {
            get => _isConfirmarEnabled;
            set
            {
                _isConfirmarEnabled = value;
                OnPropertyChanged(nameof(IsConfirmarEnabled));
            }
        }

        private void CarregarCircuitos()
        {
            try
            {
                var circuitos = _definirRotaCircuito.ObterNomesCircuitosOrdenados(_uiDocument);
                foreach (var circuito in circuitos)
                {
                    CircuitComboBox.Items.Add(circuito);
                }

                CircuitComboBox.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar circuitos: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PersonalizarButton_Click(object sender, RoutedEventArgs e)
        {
            if (CircuitComboBox.SelectedIndex < 0)
            {
                MessageBox.Show("Selecione um circuito antes de personalizar a rota.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var circuitoSelecionado = CircuitComboBox.SelectedItem.ToString();

            try
            {
                var elementosPreSelecionados = _definirRotaCircuito.CalcularRota(_uiDocument, circuitoSelecionado).ToList();
                TaskDialog.Show("Modo de Seleção", "Selecione os elementos desejados na vista do Revit e pressione 'Confirmar' para salvar.");
                _uiDocument.Selection.SetElementIds(elementosPreSelecionados);
                IsConfirmarEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao personalizar rota: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConfirmarButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsConfirmarEnabled)
            {
                MessageBox.Show("O botão Confirmar não está ativo.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CircuitComboBox.SelectedIndex < 0)
            {
                MessageBox.Show("Por favor, selecione um circuito antes de confirmar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var circuitoSelecionado = CircuitComboBox.SelectedItem.ToString();

            try
            {
                var elementosSelecionados = _uiDocument.Selection.GetElementIds().ToList();

                if (elementosSelecionados.Count > 0)
                {
                    _personalizarRotaLogic.SalvarRotaPersonalizada(circuitoSelecionado, elementosSelecionados);
                    MessageBox.Show($"Rota personalizada para o circuito '{circuitoSelecionado}' salva com sucesso!", "Sucesso", MessageBoxButton.OK);
                    IsConfirmarEnabled = false;
                }
                else
                {
                    MessageBox.Show("Nenhum elemento foi selecionado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar rota: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void CircuitComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CircuitComboBox.SelectedIndex < 0)
            {
                MessageBox.Show("Selecione um circuito.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var circuitoSelecionado = CircuitComboBox.SelectedItem.ToString();

            try
            {
                var elementosRotaPersonalizada = _personalizarRotaLogic.ObterElementosPersonalizados(circuitoSelecionado);

                if (elementosRotaPersonalizada != null && elementosRotaPersonalizada.Count > 0)
                {
                    _uiDocument.Selection.SetElementIds(elementosRotaPersonalizada);
                }
                else
                {
                    var elementosRotaCalculada = _definirRotaCircuito.CalcularRota(_uiDocument, circuitoSelecionado).ToList();
                    _uiDocument.Selection.SetElementIds(elementosRotaCalculada);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar a rota: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void RestaurarButton_Click(object sender, RoutedEventArgs e)
        {
            var circuitoSelecionado = CircuitComboBox.SelectedItem.ToString();

            try
            {
                _personalizarRotaLogic.RestaurarRota(circuitoSelecionado);
                MessageBox.Show($"Rota personalizada para o circuito '{circuitoSelecionado}' foi restaurada com sucesso.", "Sucesso", MessageBoxButton.OK);
                CircuitComboBox.SelectedIndex = -1;
                CircuitComboBox.Items.Clear();
                CarregarCircuitos();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao restaurar rota: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
