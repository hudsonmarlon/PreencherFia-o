using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using PreencherFiacao.Model;

namespace PreencherFiacao.Model
{
    public class GerenciadorDeRotas
    {
        private Document _document;
        private UIDocument _uiDocument;
        private DefinirRotaCircuito _definirRotaCircuito;

        public GerenciadorDeRotas(UIDocument uiDocument)
        {
            _uiDocument = uiDocument;
            _document = uiDocument.Document;
            _definirRotaCircuito = new DefinirRotaCircuito(uiDocument);
        }

        /// <summary>
        /// Obtém a rota de um elemento inicial até um elemento de destino usando DefinirRotaCircuito.
        /// </summary>
        public List<ElementId> CalcularRota(FamilyInstance origem, FamilyInstance destino)
        {
            if (origem == null || destino == null)
                return new List<ElementId>();

            return _definirRotaCircuito.CalcularRota(_uiDocument, origem, destino).ToList();
        }

        public int EncontrarSlotDisponivel(Conduit eletroduto, int maxSlots)
        {
            for (int i = 1; i <= maxSlots; i++)
            {
                var paramCircuito = eletroduto.LookupParameter($"MRV_#{i}_Circuito");
                if (paramCircuito != null && string.IsNullOrEmpty(paramCircuito.AsString()))
                {
                    return i;
                }
            }
            return -1; // Sem slots disponíveis
        }

    }
}
