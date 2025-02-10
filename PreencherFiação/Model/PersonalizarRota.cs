using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PreencherFiacao.Model

{
    public class PersonalizarRota
    {
        private readonly UIDocument _uiDocument;
        private Dictionary<string, List<ElementId>> _circuitosCalculados;
        private Dictionary<string, List<ElementId>> _circuitosPersonalizados;

        public PersonalizarRota(UIDocument uiDocument)
        {
            _uiDocument = uiDocument;
            _circuitosCalculados = new Dictionary<string, List<ElementId>>();
            _circuitosPersonalizados = new Dictionary<string, List<ElementId>>();
        }

        // Adicionar um novo circuito calculado
        public void AdicionarCircuitoCalculado(string circuito, ICollection<ElementId> elementosCalculados)
        {
            if (_circuitosCalculados.ContainsKey(circuito))
            {
                TaskDialog.Show("Aviso", $"O circuito '{circuito}' já existe. Atualizando os elementos.");
                _circuitosCalculados[circuito] = elementosCalculados.ToList();
            }
            else
            {
                _circuitosCalculados.Add(circuito, elementosCalculados.ToList());
            }
        }

        // Atualizar uma rota personalizada existente ou adicioná-la
        public void AtualizarCircuitoPersonalizado(string circuito, ICollection<ElementId> elementosPersonalizados)
        {
            if (_circuitosPersonalizados.ContainsKey(circuito))
            {
                _circuitosPersonalizados[circuito] = elementosPersonalizados.ToList();
            }
            else
            {
                _circuitosPersonalizados.Add(circuito, elementosPersonalizados.ToList());
            }
        }

        // Remover um circuito personalizado
        public void RemoverCircuitoPersonalizado(string circuito)
        {
            if (_circuitosPersonalizados.ContainsKey(circuito))
            {
                _circuitosPersonalizados.Remove(circuito);
            }
            else
            {
                TaskDialog.Show("Aviso", $"Não há rota personalizada para o circuito '{circuito}' para remover.");
            }
        }

        // Limpar todos os circuitos calculados
        public void LimparCircuitosCalculados()
        {
            _circuitosCalculados.Clear();
        }

        // Limpar todos os circuitos personalizados
        public void LimparCircuitosPersonalizados()
        {
            _circuitosPersonalizados.Clear();
        }

        // Obter todos os circuitos calculados
        public Dictionary<string, List<ElementId>> ObterTodosCircuitosCalculados()
        {
            return new Dictionary<string, List<ElementId>>(_circuitosCalculados);
        }

        // Obter todos os circuitos personalizados
        public Dictionary<string, List<ElementId>> ObterTodosCircuitosPersonalizados()
        {
            return new Dictionary<string, List<ElementId>>(_circuitosPersonalizados);
        }

        // Obter elementos calculados de um circuito
        public List<ElementId> ObterElementosCalculados(string circuito)
        {
            if (_circuitosCalculados.ContainsKey(circuito))
            {
                return _circuitosCalculados[circuito];
            }

            return new List<ElementId>();
        }

        // Obter elementos personalizados de um circuito
        public List<ElementId> ObterElementosPersonalizados(string circuito)
        {
            if (_circuitosPersonalizados.ContainsKey(circuito))
            {
                return _circuitosPersonalizados[circuito];
            }

            return new List<ElementId>();
        }

        internal void RestaurarRota(string circuitoSelecionado, Action value)
        {
            throw new NotImplementedException();
        }

        public void SalvarRotaPersonalizada(string circuito, ICollection<ElementId> elementosPersonalizados)
        {
            if (_circuitosPersonalizados.ContainsKey(circuito))
            {
                _circuitosPersonalizados[circuito] = elementosPersonalizados.ToList();
            }
            else
            {
                _circuitosPersonalizados.Add(circuito, elementosPersonalizados.ToList());
            }

            // Debug: Verificar se a rota personalizada foi salva corretamente
            TaskDialog.Show("Debug", $"Rota personalizada salva para o circuito: {circuito}. Total de elementos: {_circuitosPersonalizados[circuito].Count}");
        }

        internal void RestaurarRota(string circuitoSelecionado)
        {
            throw new NotImplementedException();
        }
    }
}
