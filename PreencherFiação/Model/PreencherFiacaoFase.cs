using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using static PreencherFiacao.View.PreencherFiacaoWindow;

namespace PreencherFiacao.Model
{
    public class PreencherFiacaoFase
    {
        private Document _document;

        public PreencherFiacaoFase(Document document)
        {
            _document = document;
        }

        public void PreencherFase(List<FiacaoCircuito> circuitosFiacao)
        {
            using (Transaction trans = new Transaction(_document, "Preencher Fase"))
            {
                trans.Start();

                int maxSlots = 5;
                foreach (var fiacao in circuitosFiacao)
                {
                    var eletrodutos = ObterEletrodutosDoCircuito(fiacao.Circuito);

                    foreach (var eletroduto in eletrodutos)
                    {
                        int slotDisponivel = EncontrarSlotDisponivel(eletroduto, maxSlots);
                        if (slotDisponivel == -1) continue;

                        eletroduto.LookupParameter($"MRV_#{slotDisponivel}_Circuito")?.Set(fiacao.Circuito);
                        eletroduto.LookupParameter($"MRV_#{slotDisponivel}_Fase")?.Set(fiacao.QuantidadeFase);
                    }
                }

                trans.Commit();
            }
        }

        private List<Conduit> ObterEletrodutosDoCircuito(string nomeCircuito)
        {
            return new FilteredElementCollector(_document)
                .OfClass(typeof(Conduit))
                .WhereElementIsNotElementType()
                .Cast<Conduit>()
                .Where(e => e.LookupParameter("MRV_#1_Circuito")?.AsString() == nomeCircuito)
                .ToList();
        }

        private int EncontrarSlotDisponivel(Conduit eletroduto, int maxSlots)
        {
            for (int i = 1; i <= maxSlots; i++)
            {
                var paramCircuito = eletroduto.LookupParameter($"MRV_#{i}_Circuito");
                if (paramCircuito != null && string.IsNullOrEmpty(paramCircuito.AsString()))
                {
                    return i;
                }
            }
            return -1; // Nenhum slot disponível
        }
    }
}
