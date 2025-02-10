using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PreencherFiacao.Model
{
    public class PreencherFiacao
    {
        private readonly UIDocument _uiDocument;
        private readonly string _filePathCalculados;
        private readonly string _filePathPersonalizados;

        private Dictionary<string, List<ElementId>> _circuitosCalculados;
        private Dictionary<string, List<ElementId>> _circuitosPersonalizados;

        public PreencherFiacao(UIDocument uiDocument)
        {
            _uiDocument = uiDocument;
            _filePathCalculados = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CircuitosCalculados.json");
            _filePathPersonalizados = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CircuitosPersonalizados.json");

            _circuitosCalculados = CarregarCircuitos(_filePathCalculados);
            _circuitosPersonalizados = CarregarCircuitos(_filePathPersonalizados);
        }

        public void ExecutarPreenchimento()
        {
            var doc = _uiDocument.Document;

            using (var transaction = new Transaction(doc, "Preencher Parâmetros de Fiação"))
            {
                transaction.Start();

                foreach (var circuito in ObterTodosCircuitos())
                {
                    foreach (var elementoId in circuito.Value)
                    {
                        var elemento = doc.GetElement(elementoId);

                        if (elemento is Conduit conduit)
                        {
                            PreencherParametrosConduite(conduit);
                        }
                    }
                }

                transaction.Commit();
            }

            TaskDialog.Show("Sucesso", "Os parâmetros de fiação foram preenchidos com sucesso!");
        }

        private Dictionary<string, List<ElementId>> ObterTodosCircuitos()
        {
            var todosCircuitos = new Dictionary<string, List<ElementId>>(_circuitosCalculados);

            foreach (var personalizado in _circuitosPersonalizados)
            {
                todosCircuitos[personalizado.Key] = personalizado.Value;
            }

            return todosCircuitos;
        }

        private void PreencherParametrosConduite(Conduit conduit)
        {
            var comprimentoParam = conduit.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
            var materialParam = conduit.LookupParameter("Material");

            if (comprimentoParam != null && materialParam != null)
            {
                double comprimento = comprimentoParam.AsDouble();
                materialParam.Set("PVC"); // Exemplo de preenchimento

                var correnteParam = conduit.LookupParameter("Corrente");
                if (correnteParam != null)
                {
                    correnteParam.Set(comprimento * 0.1); // Exemplo de cálculo de corrente
                }
            }
        }

        private Dictionary<string, List<ElementId>> CarregarCircuitos(string filePath)
        {
            if (!File.Exists(filePath)) return new Dictionary<string, List<ElementId>>();

            try
            {
                var json = File.ReadAllText(filePath);
                var dados = JsonConvert.DeserializeObject<Dictionary<string, List<int>>>(json);

                return dados.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(id => new ElementId(id)).ToList()
                );
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Erro", $"Erro ao carregar os dados: {ex.Message}");
                return new Dictionary<string, List<ElementId>>();
            }
        }

        private void SalvarCircuitos(Dictionary<string, List<ElementId>> circuitos, string filePath)
        {
            try
            {
                var dadosSerializaveis = circuitos
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Select(id => id.IntegerValue).ToList()
                    );

                var json = JsonConvert.SerializeObject(dadosSerializaveis, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Erro", $"Erro ao salvar os dados: {ex.Message}");
            }
        }

    }
}
