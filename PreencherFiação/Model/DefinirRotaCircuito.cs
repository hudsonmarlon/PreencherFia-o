using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace PreencherFiacao.Model
{
    [Transaction(TransactionMode.Manual)]
    public class DefinirRotaCircuito : INotifyPropertyChanged
    {
        private Dictionary<string, List<ElementId>> _circuitosCalculados;
        private bool _isConfirmarEnabled = false;
        private UIDocument _uiDocument;
        private Document _document;


        public event PropertyChangedEventHandler PropertyChanged;

        public DefinirRotaCircuito(UIDocument _uiDocument)
        {
            // Substituí o `new()` por uma chamada explícita ao construtor.
            _circuitosCalculados = new Dictionary<string, List<ElementId>>();
        }

        public DefinirRotaCircuito()
        {
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

        public List<string> ObterNomesCircuitosOrdenados(UIDocument uiDocument)
        {
            var doc = uiDocument.Document;

            var circuitos = new FilteredElementCollector(doc)
                .OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>()
                .Where(c => c.BaseEquipment != null && c.SystemType == ElectricalSystemType.PowerCircuit)
                .OrderBy(c => c.Name)
                .Select(c => c.Name)
                .ToList();

            return circuitos;
        }

        public ICollection<ElementId> CalcularRota(UIDocument uiDocument, FamilyInstance origem, FamilyInstance destino)
        {
            var doc = uiDocument.Document;

            if (origem == null || destino == null)
            {
                throw new ArgumentNullException("Origem ou destino não podem ser nulos.");
            }

            // Verifica se a origem e o destino pertencem a algum circuito
            Parameter paramCircuitoOrigem = origem.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NUMBER);
            Parameter paramCircuitoDestino = destino.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NUMBER);

            string circuitoOrigem = paramCircuitoOrigem?.AsString();
            string circuitoDestino = paramCircuitoDestino?.AsString();

            if (string.IsNullOrEmpty(circuitoOrigem) || string.IsNullOrEmpty(circuitoDestino) || circuitoOrigem != circuitoDestino)
            {
                throw new InvalidOperationException("Os elementos não pertencem ao mesmo circuito ou não possuem um circuito definido.");
            }

            // Chama a versão original do método, reutilizando a lógica existente
            return CalcularRota(uiDocument, circuitoOrigem);
        }


        public ICollection<ElementId> CalcularRota(UIDocument uiDocument, string circuitoSelecionado)
        {
            var doc = uiDocument.Document;

            var circuito = new FilteredElementCollector(doc)
                .OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>()
                .FirstOrDefault(c => c.Name == circuitoSelecionado);

            if (circuito == null || circuito.BaseEquipment == null)
            {
                throw new InvalidOperationException($"Circuito {circuitoSelecionado} ou equipamento base não encontrado.");
            }

            var elementosCircuito = new List<ElementId>();
            foreach (Element elemento in circuito.Elements)
            {
                if (elemento is FamilyInstance fi && fi.SuperComponent != null)
                {
                    elementosCircuito.Add(fi.SuperComponent.Id);
                }
                else
                {
                    elementosCircuito.Add(elemento.Id);
                }
            }

            elementosCircuito = elementosCircuito.Distinct().ToList();

            var elementosConectados = new HashSet<ElementId>();
            foreach (var elementoId in elementosCircuito)
            {
                var caminhos = ObterTodosCaminhosConectados(doc, elementoId, circuito.BaseEquipment.Id);
                if (caminhos.Count > 0)
                {
                    var caminhoMaisCurto = caminhos.OrderBy(c => CalcularComprimentoCaminho(doc, c)).FirstOrDefault();
                    elementosConectados.UnionWith(caminhoMaisCurto);
                }
            }

            return elementosConectados;
        }

        public ICollection<ElementId> CalcularRotaDispositivosEletricos(UIDocument uiDocument, string circuitoSelecionado)
        {
            var doc = uiDocument.Document;

            var circuito = new FilteredElementCollector(doc)
                .OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>()
                .FirstOrDefault(c => c.Name == circuitoSelecionado);

            if (circuito == null || circuito.BaseEquipment == null)
            {
                throw new InvalidOperationException($"Circuito {circuitoSelecionado} ou equipamento base não encontrado.");
            }

            var elementosCircuito = new List<ElementId>();

            foreach (Element elemento in circuito.Elements)
            {
                // Filtrar apenas os elementos da categoria "OST_ElectricalFixtures"
                if (elemento.Category != null && elemento.Category.Id.IntegerValue == (int)BuiltInCategory.OST_ElectricalFixtures)
                {
                    if (elemento is FamilyInstance fi && fi.SuperComponent != null)
                    {
                        elementosCircuito.Add(fi.SuperComponent.Id);
                    }
                    else
                    {
                        elementosCircuito.Add(elemento.Id);
                    }
                }
            }

            elementosCircuito = elementosCircuito.Distinct().ToList();

            var elementosConectados = new HashSet<ElementId>();

            foreach (var elementoId in elementosCircuito)
            {
                var caminhos = ObterTodosCaminhosConectados(doc, elementoId, circuito.BaseEquipment.Id);
                if (caminhos.Count > 0)
                {
                    var caminhoMaisCurto = caminhos.OrderBy(c => CalcularComprimentoCaminho(doc, c)).FirstOrDefault();
                    elementosConectados.UnionWith(caminhoMaisCurto);
                }
            }

            return elementosConectados;
        }

        public ICollection<ElementId> CalcularRotaInterruptores(UIDocument uiDocument, string circuitoSelecionado)
        {
            var doc = uiDocument.Document;

            var circuito = new FilteredElementCollector(doc)
                .OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>()
                .FirstOrDefault(c => c.Name == circuitoSelecionado);

            if (circuito == null || circuito.BaseEquipment == null)
            {
                throw new InvalidOperationException($"Circuito {circuitoSelecionado} ou equipamento base não encontrado.");
            }

            var elementosCircuito = new List<ElementId>();

            foreach (Element elemento in circuito.Elements)
            {
                // Filtrar apenas os elementos da categoria "OST_LightingDevices"
                if (elemento.Category != null && elemento.Category.Id.IntegerValue == (int)BuiltInCategory.OST_LightingDevices)
                {
                    if (elemento is FamilyInstance fi && fi.SuperComponent != null)
                    {
                        elementosCircuito.Add(fi.SuperComponent.Id);
                    }
                    else
                    {
                        elementosCircuito.Add(elemento.Id);
                    }
                }
            }

            elementosCircuito = elementosCircuito.Distinct().ToList();

            var elementosConectados = new HashSet<ElementId>();

            foreach (var elementoId in elementosCircuito)
            {
                var caminhos = ObterTodosCaminhosConectados(doc, elementoId, circuito.BaseEquipment.Id);
                if (caminhos.Count > 0)
                {
                    var caminhoMaisCurto = caminhos.OrderBy(c => CalcularComprimentoCaminho(doc, c)).FirstOrDefault();
                    elementosConectados.UnionWith(caminhoMaisCurto);
                }
            }

            return elementosConectados;
        }


        private List<List<ElementId>> ObterTodosCaminhosConectados(Document doc, ElementId elementoInicial, ElementId qdcId)
        {
            var caminhos = new List<List<ElementId>>();
            var fila = new Queue<List<ElementId>>();
            fila.Enqueue(new List<ElementId> { elementoInicial });

            while (fila.Count > 0)
            {
                var caminhoAtual = fila.Dequeue();
                var ultimoElemento = doc.GetElement(caminhoAtual.Last());

                if (ultimoElemento.Id == qdcId)
                {
                    caminhos.Add(new List<ElementId>(caminhoAtual));
                    continue;
                }

                var conectores = ObterConnectoresAtivos(ultimoElemento);

                foreach (var connector in conectores)
                {
                    foreach (Connector conectado in connector.AllRefs)
                    {
                        var conectadoElemento = conectado.Owner;
                        if (conectadoElemento != null && !caminhoAtual.Contains(conectadoElemento.Id))
                        {
                            var novoCaminho = new List<ElementId>(caminhoAtual) { conectadoElemento.Id };
                            fila.Enqueue(novoCaminho);
                        }
                    }
                }
            }

            return caminhos;
        }

        private double CalcularComprimentoCaminho(Document doc, List<ElementId> caminho)
        {
            double comprimentoTotal = 0.0;
            foreach (var id in caminho)
            {
                var elemento = doc.GetElement(id);
                if (elemento is Conduit conduit)
                {
                    var comprimentoParam = conduit.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                    if (comprimentoParam != null)
                    {
                        comprimentoTotal += comprimentoParam.AsDouble();
                    }
                }
            }
            return comprimentoTotal;
        }

        private IEnumerable<Connector> ObterConnectoresAtivos(Element elemento)
        {
            var conectoresAtivos = new List<Connector>();

            if (elemento is FamilyInstance familyInstance)
            {
                var connectorSet = familyInstance.MEPModel?.ConnectorManager?.Connectors;
                if (connectorSet != null)
                {
                    foreach (Connector connector in connectorSet)
                    {
                        if (connector.IsConnected)
                        {
                            conectoresAtivos.Add(connector);
                        }
                    }
                }
            }
            else if (elemento is Conduit conduit)
            {
                var connectorSet = conduit.ConnectorManager?.Connectors;
                if (connectorSet != null)
                {
                    foreach (Connector connector in connectorSet)
                    {
                        if (connector.IsConnected)
                        {
                            conectoresAtivos.Add(connector);
                        }
                    }
                }
            }

            return conectoresAtivos;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

       [Obsolete] public HashSet<ElementId> ObterRotaDosRetornos()
        {
            Document doc = _uiDocument.Document;
            HashSet<ElementId> rotaRetornos = new HashSet<ElementId>();

            // Dicionários para organizar interruptores e luminárias pelo Switch ID
            Dictionary<string, List<FamilyInstance>> interruptoresPorSwitchID = new Dictionary<string, List<FamilyInstance>>();
            Dictionary<string, List<FamilyInstance>> luminariasPorSwitchID = new Dictionary<string, List<FamilyInstance>>();

            // 🔍 **Coletar luminárias e associar ao Switch ID**
            var luminarias = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Where(fi => fi.Category.Name == "Luminárias")
                .Cast<FamilyInstance>();

            foreach (var luminaria in luminarias)
            {
                Parameter switchIdParam = luminaria.get_Parameter(BuiltInParameter.RBS_ELEC_SWITCH_ID_PARAM);
                string switchId = switchIdParam?.AsString() ?? "[Vazio]";

                if (!string.IsNullOrEmpty(switchId) && switchId != "[Vazio]")
                {
                    if (!luminariasPorSwitchID.ContainsKey(switchId))
                    {
                        luminariasPorSwitchID[switchId] = new List<FamilyInstance>();
                    }
                    luminariasPorSwitchID[switchId].Add(luminaria);
                }
            }

            // 🔍 **Coletar interruptores e associar ao Switch ID**
            var interruptores = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Where(fi => fi.Category.Name == "Dispositivos de iluminação")
                .Cast<FamilyInstance>();

            foreach (var interruptor in interruptores)
            {
                Parameter switchIdParam = interruptor.get_Parameter(BuiltInParameter.RBS_ELEC_SWITCH_ID_PARAM);
                string switchId = switchIdParam?.AsString() ?? "[Vazio]";

                if (!string.IsNullOrEmpty(switchId) && switchId != "[Vazio]")
                {
                    if (!interruptoresPorSwitchID.ContainsKey(switchId))
                    {
                        interruptoresPorSwitchID[switchId] = new List<FamilyInstance>();
                    }
                    interruptoresPorSwitchID[switchId].Add(interruptor);
                }
            }

            // 🔗 **Encontrar eletrodutos conectados para cada Switch ID**
            foreach (var switchId in luminariasPorSwitchID.Keys)
            {
                if (!interruptoresPorSwitchID.ContainsKey(switchId))
                {
                    continue; // Se não há interruptor correspondente, pula para o próximo
                }

                var listaInterruptores = interruptoresPorSwitchID[switchId];
                var listaLuminarias = luminariasPorSwitchID[switchId];

                foreach (var interruptor in listaInterruptores)
                {
                    foreach (var luminaria in listaLuminarias)
                    {
                        var caminho = ObterCaminhoAteLuminaria(interruptor, luminaria);
                        rotaRetornos.UnionWith(caminho);
                    }
                }
            }

            return rotaRetornos;
        }

        /// <summary>
        /// Retorna a lista de eletrodutos que conectam um interruptor a uma luminária.
        /// </summary>
        public List<ElementId> ObterCaminhoAteLuminaria(FamilyInstance interruptor, FamilyInstance luminaria)
        {
            List<ElementId> caminho = new List<ElementId>();

            // Se o interruptor estiver aninhado, subimos para a família principal
            if (interruptor != null && interruptor.SuperComponent != null)
            {
                interruptor = interruptor.SuperComponent as FamilyInstance;
            }

            if (interruptor == null || luminaria == null)
                return new List<ElementId>();  // ✅ Retorna lista vazia para evitar erro

            // 🚀 Implementação de BFS (Busca pelo menor caminho)
            Queue<List<ElementId>> fila = new Queue<List<ElementId>>();
            HashSet<ElementId> visitados = new HashSet<ElementId>();

            fila.Enqueue(new List<ElementId> { interruptor.Id });
            visitados.Add(interruptor.Id);

            while (fila.Count > 0)
            {
                var caminhoAtual = fila.Dequeue();
                var ultimoElementoId = caminhoAtual.Last();
                var ultimoElemento = _document.GetElement(ultimoElementoId);

                // ✅ Se encontrou a luminária, retorna o caminho encontrado
                if (ultimoElementoId == luminaria.Id)
                {
                    return caminhoAtual;
                }

                // Busca conectores ativos do último elemento analisado
                var conectores = ObterConnectoresAtivos(ultimoElemento);

                foreach (var connector in conectores)
                {
                    try
                    {
                        // Verifica se o conector está conectado e se é um conector físico antes de iterar
                        if (connector.IsConnected && (connector.ConnectorType == ConnectorType.End ||
                                                      connector.ConnectorType == ConnectorType.Curve ||
                                                      connector.ConnectorType == ConnectorType.Physical))
                        {
                            foreach (Connector conectado in connector.AllRefs)
                            {
                                Element conectadoElemento = conectado.Owner;
                                if (conectadoElemento != null && !visitados.Contains(conectadoElemento.Id))
                                {
                                    visitados.Add(conectadoElemento.Id);
                                    var novoCaminho = new List<ElementId>(caminhoAtual) { conectadoElemento.Id };
                                    fila.Enqueue(novoCaminho);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Erro", $"Erro ao acessar o conector: {ex.Message}");
                    }
                }
            }

            return new List<ElementId>();  // ✅ Retorna lista vazia caso não encontre caminho
        }

        public List<ElementId> ObterRotaDispositivosEletricos(UIDocument uiDocument, string circuitoSelecionado)
        {
            var rotaCompleta = CalcularRota(uiDocument, circuitoSelecionado);
            return rotaCompleta
                .Where(id => _document.GetElement(id).Category.Id.IntegerValue == (int)BuiltInCategory.OST_ElectricalFixtures)
                .ToList();
        }

        public List<ElementId> ObterRotaEletrodutos(UIDocument uiDocument, string circuitoSelecionado)
        {
            var rotaCompleta = CalcularRota(uiDocument, circuitoSelecionado);
            return rotaCompleta
                .Where(id => _document.GetElement(id) is Conduit)
                .ToList();
        }

    }
}
