using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using PreencherFiacao.View;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PreencherFiacao.Model
{
    public class ConfirmarRota
    {
        private UIDocument _uiDocument;

        // Dicionário estático para salvar as rotas calculadas automaticamente
        private static Dictionary<string, ICollection<ElementId>> _circuitosSalvos = new Dictionary<string, ICollection<ElementId>>();

        // Dicionário estático para salvar as rotas personalizadas manualmente
        private static Dictionary<string, ICollection<ElementId>> _circuitosPersonalizados = new Dictionary<string, ICollection<ElementId>>();

        private void ExibirNotificacaoTemporaria(string titulo, string mensagem, int duracaoMs = 3000)
        {
            // Cria uma nova thread para gerenciar o TaskDialog
            var thread = new System.Threading.Thread(() =>
            {
                TaskDialog td = new TaskDialog(titulo)
                {
                    MainInstruction = mensagem,
                    TitleAutoPrefix = false,
                    CommonButtons = TaskDialogCommonButtons.None
                };

                // Exibe o TaskDialog
                td.Show();

                // Aguarda pelo tempo especificado
                System.Threading.Thread.Sleep(duracaoMs);

                // Fecha o TaskDialog automaticamente
                // É necessário capturar o identificador da janela (implementação adicional)
            });

            thread.IsBackground = true;
            thread.Start();
        }



        public ConfirmarRota(UIDocument uiDocument)
        {
            _uiDocument = uiDocument;
        }

        public void Executar(string circuitoSelecionado, bool personalizar = false)
        {
            try
            {
                if (string.IsNullOrEmpty(circuitoSelecionado))
                {
                    TaskDialog.Show("Erro", "Nenhum circuito foi selecionado.");
                    return;
                }

                var doc = _uiDocument.Document;

                // Obter o circuito selecionado
                var circuito = new FilteredElementCollector(doc)
                    .OfClass(typeof(ElectricalSystem))
                    .Cast<ElectricalSystem>()
                    .FirstOrDefault(c => c.Name == circuitoSelecionado);

                if (circuito == null || circuito.BaseEquipment == null)
                {
                    TaskDialog.Show("Erro", "Circuito ou equipamento base não encontrado.");
                    return;
                }

                // Calcular a rota
                var definirRota = new DefinirRotaCircuito();
                var elementosRota = definirRota.CalcularRota(_uiDocument, circuitoSelecionado);

                if (elementosRota != null && elementosRota.Count > 0)
                {
                    if (personalizar)
                    {
                        // Salvar como rota personalizada
                        if (_circuitosPersonalizados.ContainsKey(circuitoSelecionado))
                        {
                            _circuitosPersonalizados[circuitoSelecionado] = elementosRota;
                        }
                        else
                        {
                            _circuitosPersonalizados.Add(circuitoSelecionado, elementosRota);
                        }
                        ExibirNotificacaoTemporaria("Sucesso", $"Rota personalizada para o circuito '{circuitoSelecionado}' foi salva com sucesso!",3000);
                    }
                    else
                    {
                        // Salvar como rota padrão
                        if (_circuitosSalvos.ContainsKey(circuitoSelecionado))
                        {
                            _circuitosSalvos[circuitoSelecionado] = elementosRota;
                        }
                        else
                        {
                            _circuitosSalvos.Add(circuitoSelecionado, elementosRota);
                        }
                        ExibirNotificacaoTemporaria("Sucesso", $"Rota padrão para o circuito '{circuitoSelecionado}' foi salva com sucesso!",3000);
                    }

                    // Mostrar os elementos na seleção
                    _uiDocument.Selection.SetElementIds(elementosRota);
                }
                else
                {
                    TaskDialog.Show("Erro", $"Não foi possível salvar a rota para o circuito '{circuitoSelecionado}'.");
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Erro", $"Erro ao salvar a rota: {ex.Message}");
            }
        }

        private void AbrirCircuitSelectionWindow()
        {
            // Fechar a janela atual, se necessário, e abrir a tela de seleção
            var circuitSelectionWindow = new CircuitSelectionWindow(_uiDocument);
            circuitSelectionWindow.ShowDialog();
        }

        public static Dictionary<string, ICollection<ElementId>> ObterCircuitosSalvos()
        {
            return _circuitosSalvos;
        }

        public static Dictionary<string, ICollection<ElementId>> ObterCircuitosPersonalizados()
        {
            return _circuitosPersonalizados;
        }
    }
}
