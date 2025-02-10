using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;

namespace PreencherFiacao.Model
{
    public class RotaQDC
    {
        private Document _document;

        public RotaQDC(Document document)
        {
            _document = document;
        }

        /// <summary>
        /// Método genérico para obter o caminho entre um elemento inicial e um elemento destino, utilizando BFS.
        /// </summary>
        public List<ElementId> ObterCaminho(Element elementoInicial, Element elementoDestino)
        {
            List<ElementId> caminho = new List<ElementId>();

            if (elementoInicial is FamilyInstance fiInicial && fiInicial.SuperComponent != null)
                elementoInicial = fiInicial.SuperComponent as Element;
            if (elementoDestino is FamilyInstance fiDestino && fiDestino.SuperComponent != null)
                elementoDestino = fiDestino.SuperComponent as Element;

            if (elementoInicial == null || elementoDestino == null)
            {
                TaskDialog.Show("Erro", "Elemento inicial ou destino é nulo ou inválido.");
                return caminho;
            }

            Queue<List<ElementId>> fila = new Queue<List<ElementId>>();
            HashSet<ElementId> visitados = new HashSet<ElementId>();

            fila.Enqueue(new List<ElementId> { elementoInicial.Id });
            visitados.Add(elementoInicial.Id);

            while (fila.Count > 0)
            {
                var caminhoAtual = fila.Dequeue();
                Element ultimoElemento = _document.GetElement(caminhoAtual.Last());

                if (ultimoElemento.Id == elementoDestino.Id)
                    return caminhoAtual;

                var conectores = ObterConnectoresAtivos(ultimoElemento);

                foreach (var connector in conectores)
                {
                    if (!connector.IsConnected) continue;

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

            return new List<ElementId>();
        }

        /// <summary>
        /// Obtém os conectores ativos de um elemento (FamilyInstance ou Conduit).
        /// </summary>
        private IEnumerable<Connector> ObterConnectoresAtivos(Element elemento)
        {
            List<Connector> conectoresAtivos = new List<Connector>();

            if (elemento is FamilyInstance familyInstance)
            {
                if (familyInstance.SuperComponent != null)
                    elemento = familyInstance.SuperComponent;

                var connectorSet = (elemento as FamilyInstance)?.MEPModel?.ConnectorManager?.Connectors;
                if (connectorSet != null)
                {
                    foreach (Connector connector in connectorSet)
                    {
                        if (connector.IsConnected)
                            conectoresAtivos.Add(connector);
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
                            conectoresAtivos.Add(connector);
                    }
                }
            }
            return conectoresAtivos;
        }

        /// <summary>
        /// Preenche os parâmetros dos eletrodutos corretamente.
        /// </summary>
        public void PreencherParametrosEletrodutos(HashSet<ElementId> rotaCircuito, string circuito)
        {
            using (Transaction trans = new Transaction(_document, "Preencher Parâmetros de Fiação"))
            {
                trans.Start();

                int maxSlots = 5;

                foreach (var elementoId in rotaCircuito)
                {
                    Element elemento = _document.GetElement(elementoId);
                    if (elemento is Conduit eletroduto)
                    {
                        for (int i = 1; i <= maxSlots; i++)
                        {
                            var paramCircuito = eletroduto.LookupParameter($"MRV_#{i}_Circuito");

                            if (paramCircuito != null && string.IsNullOrEmpty(paramCircuito.AsString()))
                            {
                                paramCircuito.Set(circuito);
                                break;
                            }
                        }

                        eletroduto.LookupParameter($"MRV_#{maxSlots}_Neutro")?.Set(1);
                    }
                }

                trans.Commit();
            }
        }
    }
}
