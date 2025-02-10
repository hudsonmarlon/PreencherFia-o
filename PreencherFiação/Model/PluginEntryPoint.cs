using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using static PreencherFiacao.Model.DefinirRotaCircuito;
using PreencherFiacao.View;
using System;

namespace PreencherFiacao.Model
{
    [Transaction(TransactionMode.Manual)] // Define que as transações são controladas manualmente
    public class PluginEntryPoint : IExternalCommand
    {
        // Método obrigatório da interface IExternalCommand
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Obtém o UIDocument atual (documento ativo no Revit)
                UIDocument uiDoc = commandData.Application.ActiveUIDocument;

                // Instancia e exibe a janela principal
                MainWindow mainWindow = new MainWindow(uiDoc);
                mainWindow.ShowDialog();

                // Retorna sucesso ao Revit
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                // Caso ocorra um erro, exibe a mensagem e retorna falha
                TaskDialog.Show("Erro", $"Ocorreu um erro: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}
