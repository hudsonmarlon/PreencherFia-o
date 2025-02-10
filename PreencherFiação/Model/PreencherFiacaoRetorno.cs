using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;

namespace PreencherFiacao.Model
{
    [Transaction(TransactionMode.Manual)]
    public class PreencherFiacaoRetorno
    {
        private UIDocument _uiDocument;
        private Document _document;

        // Constantes para nomes das categorias
        private const string CategoriaLuminarias = "Luminárias";
        private const string CategoriaInterruptores = "Dispositivos de iluminação";

        public PreencherFiacaoRetorno(UIDocument uiDocument)
        {
            _uiDocument = uiDocument;
            _document = uiDocument.Document;
        }

        #region Destacar Rotas

        /// <summary>
        /// Destaca a rota de retorno considerando um único interruptor conectado às luminárias.
        /// </summary>
        public void RotaRetornoSimples()
        {
            var luminariasPorSwitchID = ObterElementosPorSwitchID(_document, CategoriaLuminarias);
            var interruptoresPorSwitchID = ObterElementosPorSwitchID(_document, CategoriaInterruptores);

            foreach (var switchId in luminariasPorSwitchID.Keys)
            {
                if (!interruptoresPorSwitchID.ContainsKey(switchId))
                    continue; // Não há interruptor correspondente

                var listaInterruptores = interruptoresPorSwitchID[switchId];
                var listaLuminarias = luminariasPorSwitchID[switchId];

                // Considera apenas casos com exatamente 1 interruptor
                if (listaInterruptores.Count != 1)
                    continue;

                FamilyInstance unicoInterruptor = listaInterruptores.First();
                HashSet<ElementId> rotaCompleta = new HashSet<ElementId>();

                foreach (var luminaria in listaLuminarias)
                {
                    var caminho = ObterCaminhoAteLuminaria(unicoInterruptor, luminaria);
                    rotaCompleta.UnionWith(caminho);
                }

                if (rotaCompleta.Count > 0)
                {
                    _uiDocument.Selection.SetElementIds(rotaCompleta.ToList());
                    TaskDialog.Show("Rotas de Retorno", $"Rota destacada para Switch ID: {switchId}\nElementos na rota: {rotaCompleta.Count}");
                }
            }
        }

        /// <summary>
        /// Destaca a rota de retorno em configuração Paralela (dois interruptores).
        /// </summary>
        public void RotaRetornoParalelo()
        {
            var luminariasPorSwitchID = ObterElementosPorSwitchID(_document, CategoriaLuminarias);
            var interruptoresPorSwitchID = ObterElementosPorSwitchID(_document, CategoriaInterruptores);

            foreach (var switchId in luminariasPorSwitchID.Keys)
            {
                if (!interruptoresPorSwitchID.ContainsKey(switchId))
                    continue;

                var listaInterruptores = interruptoresPorSwitchID[switchId];
                var listaLuminarias = luminariasPorSwitchID[switchId];

                // Considera apenas casos com exatamente 2 interruptores
                if (listaInterruptores.Count != 2)
                    continue;

                FamilyInstance primeiroInterruptor = listaInterruptores[0];
                FamilyInstance segundoInterruptor = listaInterruptores[1];

                HashSet<ElementId> rotaInterruptorParaInterruptor = new HashSet<ElementId>();
                HashSet<ElementId> rotaInterruptorParaLuminaria = new HashSet<ElementId>();

                // Rota entre os interruptores
                var caminhoInterruptores = ObterCaminhoAteInterruptor(primeiroInterruptor, segundoInterruptor);
                rotaInterruptorParaInterruptor.UnionWith(caminhoInterruptores);

                // Rota do segundo interruptor até cada luminária
                foreach (var luminaria in listaLuminarias)
                {
                    var caminhoLuminaria = ObterCaminhoAteLuminaria(segundoInterruptor, luminaria);
                    rotaInterruptorParaLuminaria.UnionWith(caminhoLuminaria);
                }

                if (rotaInterruptorParaInterruptor.Count > 0)
                {
                    _uiDocument.Selection.SetElementIds(rotaInterruptorParaInterruptor.ToList());
                    TaskDialog.Show("Rotas de Retorno - Interruptores", $"Rota destacada entre os interruptores para Switch ID: {switchId}\nElementos na rota: {rotaInterruptorParaInterruptor.Count}");
                }
                if (rotaInterruptorParaLuminaria.Count > 0)
                {
                    _uiDocument.Selection.SetElementIds(rotaInterruptorParaLuminaria.ToList());
                    TaskDialog.Show("Rotas de Retorno - Luminária", $"Rota destacada do segundo interruptor até as luminárias para Switch ID: {switchId}\nElementos na rota: {rotaInterruptorParaLuminaria.Count}");
                }
            }
        }

        /// <summary>
        /// Destaca a rota de retorno para configuração Four-Way (3 interruptores).
        /// </summary>
        public void RotaRetornoFourWay()
        {
            Document doc = _uiDocument.Document;

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

                // ✅ **Segmentação: Considerar apenas os casos com exatamente três interruptores**
                if (listaInterruptores.Count != 3)
                {
                    continue; // Pula os casos onde há menos ou mais de três interruptores para o mesmo Switch ID
                }

                FamilyInstance primeiroInterruptor = listaInterruptores[0];
                FamilyInstance segundoInterruptor = listaInterruptores[1];
                FamilyInstance terceiroInterruptor = listaInterruptores[2];

                HashSet<ElementId> rotaInterruptorParaInterruptor1 = new HashSet<ElementId>();
                HashSet<ElementId> rotaInterruptorParaInterruptor2 = new HashSet<ElementId>();
                HashSet<ElementId> rotaInterruptorParaLuminaria = new HashSet<ElementId>();

                // **Primeiro trecho: Do primeiro interruptor ao segundo**
                var caminhoInterruptores1 = ObterCaminhoAteInterruptor(primeiroInterruptor, segundoInterruptor);
                rotaInterruptorParaInterruptor1.UnionWith(caminhoInterruptores1);

                // **Segundo trecho: Do segundo interruptor ao terceiro**
                var caminhoInterruptores2 = ObterCaminhoAteInterruptor(segundoInterruptor, terceiroInterruptor);
                rotaInterruptorParaInterruptor2.UnionWith(caminhoInterruptores2);

                // **Terceiro trecho: Do terceiro interruptor até a luminária**
                foreach (var luminaria in listaLuminarias)
                {
                    var caminhoLuminarias = ObterCaminhoAteLuminaria(terceiroInterruptor, luminaria);
                    rotaInterruptorParaLuminaria.UnionWith(caminhoLuminarias);
                }

                // ✅ **Selecionar as rotas no Revit separadamente**
                if (rotaInterruptorParaInterruptor1.Count > 0)
                {
                    _uiDocument.Selection.SetElementIds(rotaInterruptorParaInterruptor1.ToList());
                    TaskDialog.Show("Rotas de Retorno - Interruptores", $"Rota destacada entre o primeiro e o segundo interruptor para Switch ID: {switchId}\nElementos na rota: {rotaInterruptorParaInterruptor1.Count}");
                }

                if (rotaInterruptorParaInterruptor2.Count > 0)
                {
                    _uiDocument.Selection.SetElementIds(rotaInterruptorParaInterruptor2.ToList());
                    TaskDialog.Show("Rotas de Retorno - Interruptores", $"Rota destacada entre o segundo e o terceiro interruptor para Switch ID: {switchId}\nElementos na rota: {rotaInterruptorParaInterruptor2.Count}");
                }

                if (rotaInterruptorParaLuminaria.Count > 0)
                {
                    _uiDocument.Selection.SetElementIds(rotaInterruptorParaLuminaria.ToList());
                    TaskDialog.Show("Rotas de Retorno - Luminária", $"Rota destacada do terceiro interruptor até as luminárias para Switch ID: {switchId}\nElementos na rota: {rotaInterruptorParaLuminaria.Count}");
                }
            }
        }


        #endregion

        #region Preenchimento de Parâmetros de Fiação

        /// <summary>
        /// Preenche os parâmetros de retorno (Circuito e Retorno) nos eletrodutos.
        /// </summary>
        public void PreencherRetornos()
        {
            try
            {

                var preencherRetorno = new PreencherFiacaoRetorno(_uiDocument);
                var gerenciadorRotas = new GerenciadorDeRotas(_uiDocument);

                foreach (var switchId in preencherRetorno.ObterElementosPorSwitchID(_document, "Dispositivos de iluminação").Keys)
                {
                    var interruptores = preencherRetorno.ObterElementosPorSwitchID(_document, "Dispositivos de iluminação")[switchId];

                    if (interruptores.Count == 0)
                        continue;

                    FamilyInstance primeiroInterruptor = interruptores.First();
                    FamilyInstance ultimoInterruptor = interruptores.Last();

                    var eletrodutos = gerenciadorRotas.CalcularRota(primeiroInterruptor, ultimoInterruptor)
                        .Select(id => _document.GetElement(id))
                        .OfType<Conduit>()
                        .ToList();

                    foreach (var eletroduto in eletrodutos)
                    {
                        int slotDisponivel = gerenciadorRotas.EncontrarSlotDisponivel(eletroduto, 5);
                        if (slotDisponivel == -1) continue;

                        eletroduto.LookupParameter($"MRV_#{slotDisponivel}_Retorno")?.Set(2);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao preencher retornos: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        #endregion

        #region Métodos Auxiliares

        /// <summary>
        /// Retorna um dicionário de FamilyInstance agrupados por Switch ID para a categoria especificada.
        /// </summary>
        public Dictionary<string, List<FamilyInstance>> ObterElementosPorSwitchID(Document doc, string categoria)
        {
            var elementosPorSwitchID = new Dictionary<string, List<FamilyInstance>>();

            var colecao = new FilteredElementCollector(doc)
                            .OfClass(typeof(FamilyInstance))
                            .WhereElementIsNotElementType()
                            .Where(fi => fi.Category != null && fi.Category.Name.Equals(categoria, StringComparison.OrdinalIgnoreCase))
                            .Cast<FamilyInstance>();

            foreach (var fi in colecao)
            {
                Parameter param = fi.get_Parameter(BuiltInParameter.RBS_ELEC_SWITCH_ID_PARAM);
                string switchId = param?.AsString()?.Trim() ?? "[Vazio]";
                if (!string.IsNullOrEmpty(switchId) && switchId != "[Vazio]")
                {
                    if (!elementosPorSwitchID.ContainsKey(switchId))
                        elementosPorSwitchID[switchId] = new List<FamilyInstance>();
                    elementosPorSwitchID[switchId].Add(fi);
                }
            }
            return elementosPorSwitchID;
        }

        /// <summary>
        /// Retorna o caminho (lista de ElementIds) do interruptor até a luminária utilizando busca em largura (BFS).
        /// </summary>
        public List<ElementId> ObterCaminhoAteLuminaria(FamilyInstance interruptor, FamilyInstance luminaria)
        {
            List<ElementId> caminho = new List<ElementId>();

            // Se o interruptor estiver aninhado, pega o SuperComponent
            if (interruptor != null && interruptor.SuperComponent != null)
            {
                interruptor = interruptor.SuperComponent as FamilyInstance;
            }

            if (interruptor == null || luminaria == null)
                return new List<ElementId>();

            Queue<List<ElementId>> fila = new Queue<List<ElementId>>();
            HashSet<ElementId> visitados = new HashSet<ElementId>();

            fila.Enqueue(new List<ElementId> { interruptor.Id });
            visitados.Add(interruptor.Id);

            while (fila.Count > 0)
            {
                var caminhoAtual = fila.Dequeue();
                var ultimoElementoId = caminhoAtual.Last();
                Element ultimoElemento = _document.GetElement(ultimoElementoId);

                if (ultimoElementoId == luminaria.Id)
                {
                    return caminhoAtual;
                }

                var conectores = ObterConnectoresAtivos(ultimoElemento);

                foreach (var connector in conectores)
                {
                    try
                    {
                        if (connector.IsConnected &&
                           (connector.ConnectorType == ConnectorType.End ||
                            connector.ConnectorType == ConnectorType.Curve ||
                            connector.ConnectorType.ToString() == "Physical"))
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
                    catch (Exception)
                    {
                        // Opcional: registrar/logar exceções se necessário
                    }
                }
            }

            return new List<ElementId>();
        }

        /// <summary>
        /// Retorna o caminho (lista de ElementIds) entre dois interruptores utilizando busca em largura (BFS).
        /// </summary>
        private List<ElementId> ObterCaminhoAteInterruptor(FamilyInstance interruptorInicial, FamilyInstance interruptorDestino)
        {
            List<ElementId> caminho = new List<ElementId>();

            if (interruptorInicial != null && interruptorInicial.SuperComponent != null)
                interruptorInicial = interruptorInicial.SuperComponent as FamilyInstance;
            if (interruptorDestino != null && interruptorDestino.SuperComponent != null)
                interruptorDestino = interruptorDestino.SuperComponent as FamilyInstance;

            if (interruptorInicial == null || interruptorDestino == null)
            {
                TaskDialog.Show("Erro", "Um dos interruptores é nulo ou inválido.");
                return caminho;
            }

            Queue<List<ElementId>> fila = new Queue<List<ElementId>>();
            HashSet<ElementId> visitados = new HashSet<ElementId>();

            fila.Enqueue(new List<ElementId> { interruptorInicial.Id });
            visitados.Add(interruptorInicial.Id);

            while (fila.Count > 0)
            {
                var caminhoAtual = fila.Dequeue();
                var ultimoElementoId = caminhoAtual.Last();
                Element ultimoElemento = _document.GetElement(ultimoElementoId);

                if (ultimoElementoId == interruptorDestino.Id)
                {
                    return caminhoAtual;
                }

                var conectores = ObterConnectoresAtivos(ultimoElemento);

                foreach (var connector in conectores)
                {
                    try
                    {
                        if (!connector.IsConnected)
                            continue;

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
                    catch (Exception)
                    {
                        // Opcional: registrar/logar exceções se necessário
                    }
                }
            }

            TaskDialog.Show("Erro", $"Nenhum caminho encontrado entre Interruptor {interruptorInicial.Id} e Interruptor {interruptorDestino.Id}.");
            return new List<ElementId>();
        }

        /// <summary>
        /// Retorna os conectores ativos (usados na busca) de um elemento.
        /// </summary>
        private IEnumerable<Connector> ObterConnectoresAtivos(Element elemento)
        {
            List<Connector> conectoresAtivos = new List<Connector>();

            if (elemento is FamilyInstance familyInstance)
            {
                if (familyInstance.SuperComponent != null)
                {
                    elemento = familyInstance.SuperComponent;
                }

                var connectorSet = (elemento as FamilyInstance)?.MEPModel?.ConnectorManager?.Connectors;
                if (connectorSet != null)
                {
                    foreach (Connector connector in connectorSet)
                    {
                        try
                        {
                            if (connector.IsConnected &&
                               (connector.ConnectorType == ConnectorType.End ||
                                connector.ConnectorType == ConnectorType.Curve ||
                                connector.ConnectorType.ToString() == "Physical"))
                            {
                                conectoresAtivos.Add(connector);
                            }
                        }
                        catch (Exception)
                        {
                            // Opcional: registrar/logar exceções se necessário
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
                        try
                        {
                            if (connector.IsConnected &&
                               (connector.ConnectorType == ConnectorType.End ||
                                connector.ConnectorType == ConnectorType.Curve ||
                                connector.ConnectorType.ToString() == "Physical"))
                            {
                                conectoresAtivos.Add(connector);
                            }
                        }
                        catch (Exception)
                        {
                            // Opcional: registrar/logar exceções se necessário
                        }
                    }
                }
            }
            return conectoresAtivos;
        }

        /// <summary>
        /// Conta o número de interruptores por Switch ID no projeto.
        /// </summary>
        private Dictionary<string, int> ContarInterruptoresPorSwitchID()
        {
            Dictionary<string, int> contagemInterruptores = new Dictionary<string, int>();

            var interruptores = new FilteredElementCollector(_document)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Where(fi => fi.Category != null && fi.Category.Name.Equals(CategoriaInterruptores, StringComparison.OrdinalIgnoreCase))
                .Cast<FamilyInstance>();

            foreach (var interruptor in interruptores)
            {
                Parameter switchIdParam = interruptor.get_Parameter(BuiltInParameter.RBS_ELEC_SWITCH_ID_PARAM);
                string switchId = switchIdParam?.AsString()?.Trim() ?? "[Vazio]";
                if (!string.IsNullOrEmpty(switchId) && switchId != "[Vazio]")
                {
                    if (!contagemInterruptores.ContainsKey(switchId))
                        contagemInterruptores[switchId] = 0;
                    contagemInterruptores[switchId]++;
                }
            }

            return contagemInterruptores;
        }

        /// <summary>
        /// Calcula a quantidade de retornos com base na contagem de interruptores para o Switch ID.
        /// </summary>
        private int CalcularQuantidadeRetornos(string switchId, Dictionary<string, int> contagemInterruptores)
        {
            if (!contagemInterruptores.ContainsKey(switchId))
            {
                return 1; // Caso não encontrado, assume retorno simples
            }

            int numeroInterruptores = contagemInterruptores[switchId];

            if (numeroInterruptores == 1)
                return 1;
            else if (numeroInterruptores == 2)
                return 2;
            else if (numeroInterruptores >= 3)
                return 2;

            return 1;
        }

        #endregion
    }
}
