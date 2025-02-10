using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using PreencherFiacao.Model;

namespace PreencherFiacao.View
{
    public partial class PreencherFiacaoWindow : Window
    {
        private UIDocument _uiDocument;
        private Document _document;
        private List<FiacaoCircuito> _circuitosFiacao;
        private const int maxSlots = 5;
        private GerenciadorDeRotas _gerenciadorRotas;


        public PreencherFiacaoWindow(UIDocument uiDocument)
        {
            InitializeComponent();
            _uiDocument = uiDocument;
            _document = uiDocument.Document;
            _circuitosFiacao = new List<FiacaoCircuito>();

            CarregarDadosCircuitos();
        }

        private void CarregarDadosCircuitos()
        {
            try
            {
                var circuitos = new FilteredElementCollector(_document)
                    .OfClass(typeof(ElectricalSystem))
                    .Cast<ElectricalSystem>()
                    .Where(c => c.BaseEquipment != null && c.SystemType == ElectricalSystemType.PowerCircuit)
                    .OrderBy(c => c.Name);

                foreach (var circuito in circuitos)
                {
                    var novaFiacao = new FiacaoCircuito
                    {
                        Circuito = circuito.Name,
                        BitolaFaseNeutro = CalcularBitolaFaseNeutro(circuito),
                        BitolaTerra = CalcularBitolaTerra(circuito),
                        QuantidadeFase = CalcularQuantidadeFase(circuito),
                        QuantidadeNeutro = CalcularQuantidadeNeutro(circuito),
                        QuantidadeTerra = CalcularQuantidadeTerra(circuito)
                    };

                    _circuitosFiacao.Add(novaFiacao);
                }

                FiacaoDataGrid.ItemsSource = _circuitosFiacao;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar circuitos: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string CalcularBitolaFaseNeutro(ElectricalSystem circuito)
        {
            double corrente = circuito.get_Parameter(BuiltInParameter.RBS_ELEC_APPARENT_CURRENT_PARAM).AsDouble();
            if (corrente <= 15) return "1,5";
            if (corrente <= 20) return "2,5";
            if (corrente <= 30) return "4,0";
            if (corrente <= 40) return "6,0";
            return "10,0";
        }

        private string CalcularBitolaTerra(ElectricalSystem circuito)
        {
            double corrente = circuito.get_Parameter(BuiltInParameter.RBS_ELEC_APPARENT_CURRENT_PARAM).AsDouble();
            if (corrente <= 20) return "2,5";
            if (corrente <= 40) return "4,0";
            return "6,0";
        }

        private int CalcularQuantidadeFase(ElectricalSystem circuito)
        {
            Parameter param = circuito.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_WIRE_NUM_HOTS_PARAM);
            return param != null ? param.AsInteger() : 0;
        }

        private int CalcularQuantidadeNeutro(ElectricalSystem circuito)
        {
            Parameter param = circuito.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_WIRE_NUM_NEUTRALS_PARAM);
            return param != null ? param.AsInteger() : 0;
        }

        private int CalcularQuantidadeTerra(ElectricalSystem circuito)
        {
            Parameter param = circuito.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_WIRE_NUM_GROUNDS_PARAM);
            return param != null ? param.AsInteger() : 0;
        }

        // Método para converter pés para a unidade correta do projeto
        private double ConverterPesParaUnidade(double valorEmPes, Parameter param)
        {
            if (param == null) return valorEmPes;

            ForgeTypeId unidade = param.GetUnitTypeId();

            if (unidade == UnitTypeId.Millimeters)
                return valorEmPes / 304.8;
            else if (unidade == UnitTypeId.Centimeters)
                return valorEmPes / 30.48;
            else if (unidade == UnitTypeId.Meters)
                return valorEmPes / 0.3048;

            return valorEmPes;
        }

        private void LimparParametros_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (Transaction trans = new Transaction(_document, "Limpar Preenchimentos de Fiação"))
                {
                    trans.Start();

                    var eletrodutos = new FilteredElementCollector(_document)
                        .OfClass(typeof(Conduit))
                        .WhereElementIsNotElementType()
                        .Cast<Conduit>()
                        .ToList();

                    foreach (var eletroduto in eletrodutos)
                    {
                        for (int i = 1; i <= 5; i++)
                        {
                            var paramCircuito = eletroduto.LookupParameter($"MRV_#{i}_Circuito");
                            var paramBitolaFaseNeutro = eletroduto.LookupParameter($"MRV_#{i}_Bitola-Fase/Neutro");
                            var paramBitolaTerra = eletroduto.LookupParameter($"MRV_#{i}_Bitola-Terra");
                            var paramFase = eletroduto.LookupParameter($"MRV_#{i}_Fase");
                            var paramNeutro = eletroduto.LookupParameter($"MRV_#{i}_Neutro");
                            var paramTerra = eletroduto.LookupParameter($"MRV_#{i}_Terra");
                            var paramRetorno = eletroduto.LookupParameter($"MRV_#{i}_Retorno");

                            // Só limpa se já tiver valor aplicado
                            if (paramCircuito != null && !string.IsNullOrEmpty(paramCircuito.AsString()))
                                paramCircuito.Set("");

                            if (paramBitolaFaseNeutro != null && paramBitolaFaseNeutro.AsDouble() > 0)
                                paramBitolaFaseNeutro.Set(0.0);

                            if (paramBitolaTerra != null && paramBitolaTerra.AsDouble() > 0)
                                paramBitolaTerra.Set(0.0);

                            if (paramFase != null && paramFase.AsInteger() > 0)
                                paramFase.Set(0);

                            if (paramNeutro != null && paramNeutro.AsInteger() > 0)
                                paramNeutro.Set(0);

                            if (paramTerra != null && paramTerra.AsInteger() > 0)
                                paramTerra.Set(0);

                            if (paramRetorno != null && paramRetorno.AsInteger() > 0)
                                paramRetorno.Set(0);
                        }
                    }

                    trans.Commit();
                    MessageBox.Show("Preenchimentos removidos apenas onde havia valores aplicados!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao limpar preenchimentos: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Confirmar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (Transaction trans = new Transaction(_document, "Preencher Parâmetros de Fiação"))
                {
                    trans.Start();

                    List<string> circuitosSemEletroduto = new List<string>();
                    int maxSlots = 5;

                    // ✅ **Passo 1: Preencher dispositivos elétricos (Circuito, Fase, Neutro, Terra)**
                    PreencherDispositivosEletricos();

                    // ✅ **Passo 2: Garantir que os primeiros interruptores tenham fase**
                    PreencherFaseNosInterruptores();

                    // ✅ **Passo 3: Preencher os retornos nos eletrodutos**
                   // var preencherRetorno = new PreencherFiacao.Model.PreencherFiacaoRetorno(_uiDocument);
                    //preencherRetorno.PreencherRetornos();

                    trans.Commit();
                    MessageBox.Show("Todos os parâmetros foram preenchidos com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao preencher parâmetros: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreencherDispositivosEletricos()
        {
            try
            {

                {

                    var rotaCircuito = new PreencherFiacao.Model.DefinirRotaCircuito(_uiDocument);
                    List<string> circuitosSemEletroduto = new List<string>();
                    var gerenciadorRotas = new GerenciadorDeRotas(_uiDocument);

                    foreach (var fiacao in _circuitosFiacao)
                    {
                        // Obter a rota apenas para dispositivos elétricos
                        var elementosConectados = rotaCircuito.CalcularRotaDispositivosEletricos(_uiDocument, fiacao.Circuito)
                            .Select(id => _document.GetElement(id))
                            .OfType<Conduit>()
                            .ToList();

                        if (elementosConectados.Count == 0)
                        {
                            circuitosSemEletroduto.Add(fiacao.Circuito);
                            continue;
                        }

                        List<int> eletrodutosSemSlot = new List<int>();

                        foreach (var eletroduto in elementosConectados)
                        {
                            int slotDisponivel = gerenciadorRotas.EncontrarSlotDisponivel(eletroduto, maxSlots);

                            if (slotDisponivel == -1)
                            {
                                eletrodutosSemSlot.Add(eletroduto.Id.IntegerValue);
                                continue;
                            }

                            eletroduto.LookupParameter($"MRV_#{slotDisponivel}_Circuito")?.Set(fiacao.Circuito);
                            eletroduto.LookupParameter($"MRV_#{slotDisponivel}_Fase")?.Set(fiacao.QuantidadeFase);
                            eletroduto.LookupParameter($"MRV_#{slotDisponivel}_Neutro")?.Set(fiacao.QuantidadeNeutro);
                            eletroduto.LookupParameter($"MRV_#{slotDisponivel}_Terra")?.Set(fiacao.QuantidadeTerra);
                        }

                        if (eletrodutosSemSlot.Count > 0)
                        {
                            MessageBox.Show($"Sem slots disponíveis nos eletrodutos: {string.Join(", ", eletrodutosSemSlot)}",
                                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao preencher dispositivos elétricos: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreencherFaseNosInterruptores()
        {
            try
            {
                var rotaCircuito = new PreencherFiacao.Model.DefinirRotaCircuito(_uiDocument);
                List<string> circuitosSemEletroduto = new List<string>();
                var gerenciadorRotas = new GerenciadorDeRotas(_uiDocument);

                foreach (var fiacao in _circuitosFiacao)
                {
                    // Obter a rota apenas para dispositivos de iluminação (Lighting Devices)
                    var elementosConectados = rotaCircuito.CalcularRotaInterruptores(_uiDocument, fiacao.Circuito)
                        .Select(id => _document.GetElement(id))
                        .OfType<Conduit>()
                        .ToList();

                    if (elementosConectados.Count == 0)
                    {
                        circuitosSemEletroduto.Add(fiacao.Circuito);
                        continue;
                    }

                    List<int> eletrodutosSemSlot = new List<int>();

                    foreach (var eletroduto in elementosConectados)
                    {
                        bool circuitoJaExiste = false;
                        int slotDisponivel = -1;

                        // Verificar os slots disponíveis e se o circuito já está preenchido
                        for (int i = 1; i <= maxSlots; i++)
                        {
                            var paramCircuito = eletroduto.LookupParameter($"MRV_#{i}_Circuito");

                            if (paramCircuito != null)
                            {
                                string circuitoExistente = paramCircuito.AsString();

                                // Se já existe o mesmo circuito no eletroduto, marcamos como existente
                                if (!string.IsNullOrEmpty(circuitoExistente) && circuitoExistente == fiacao.Circuito)
                                {
                                    circuitoJaExiste = true;
                                    break; // Já existe o circuito, não precisa preencher
                                }
                                else if (string.IsNullOrEmpty(circuitoExistente) && slotDisponivel == -1)
                                {
                                    slotDisponivel = i; // Primeiro slot vazio encontrado
                                }
                            }
                        }

                        // Se o circuito já existe, não preenche novamente
                        if (circuitoJaExiste)
                        {
                            continue;
                        }

                        // Se encontramos um slot disponível, realizamos o preenchimento
                        if (slotDisponivel != -1)
                        {
                            eletroduto.LookupParameter($"MRV_#{slotDisponivel}_Circuito")?.Set(fiacao.Circuito);
                            eletroduto.LookupParameter($"MRV_#{slotDisponivel}_Fase")?.Set(fiacao.QuantidadeFase);
                        }
                    }


                    if (eletrodutosSemSlot.Count > 0)
                    {
                        MessageBox.Show($"Sem slots disponíveis nos eletrodutos: {string.Join(", ", eletrodutosSemSlot)}",
                            "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao preencher dispositivos de iluminação: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private List<ElementId> DefinirRotaParaQDC(FamilyInstance qdc, FamilyInstance destino)
        {
            var rota = new PreencherFiacao.Model.RotaQDC(_document);
            return rota.ObterCaminho(qdc, destino);
        }

        private bool VerificarFaseNoInterruptor(FamilyInstance interruptor)
        {
            Parameter paramFase = interruptor.LookupParameter("MRV_#1_Fase");
            return paramFase != null && paramFase.AsInteger() > 0;
        }


        private string ObterSwitchIDPorCircuito(string nomeCircuito)
        {
            var circuito = new FilteredElementCollector(_document)
                .OfClass(typeof(ElectricalSystem))
                .Cast<ElectricalSystem>()
                .FirstOrDefault(c => c.Name == nomeCircuito);

            return circuito?.Elements
                .OfType<FamilyInstance>()
                .Select(fi => fi.get_Parameter(BuiltInParameter.RBS_ELEC_SWITCH_ID_PARAM)?.AsString())
                .FirstOrDefault(switchId => !string.IsNullOrEmpty(switchId)) ?? "[Vazio]";
        }


        /// <summary>
        /// Conta quantos interruptores existem para cada Switch ID no projeto.
        /// </summary>
        private Dictionary<string, int> ContarInterruptoresPorSwitchID()
        {
            Document doc = _uiDocument.Document;
            Dictionary<string, int> contagemInterruptores = new Dictionary<string, int>();

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
                    if (!contagemInterruptores.ContainsKey(switchId))
                    {
                        contagemInterruptores[switchId] = 0;
                    }
                    contagemInterruptores[switchId]++;
                }
            }

            return contagemInterruptores;
        }

        /// <summary>
        /// Determina a quantidade de condutores de retorno com base no número de interruptores no circuito.
        /// </summary>
        private int CalcularQuantidadeRetornos(string switchId, Dictionary<string, int> contagemInterruptores)
        {
            if (!contagemInterruptores.ContainsKey(switchId))
            {
                return 1; // Se não encontrarmos o switch ID, assumimos um retorno simples
            }

            int numeroInterruptores = contagemInterruptores[switchId];

            if (numeroInterruptores == 1)
            {
                return 1; // Interruptor Simples: 1 retorno do interruptor até as luminárias
            }
            else if (numeroInterruptores == 2)
            {
                return 2; // Interruptor Paralelo (Three Way): 2 retornos entre os interruptores, 1 até a luminária
            }
            else if (numeroInterruptores >= 3)
            {
                return 2; // Interruptor Four Way: 2 retornos entre interruptores, 1 até a luminária
            }

            return 1; // Caso padrão
        }



        private void PreencherRetornos()
        {
            try
            {


                var eletrodutos = new FilteredElementCollector(_document)
                    .OfClass(typeof(Conduit))
                    .WhereElementIsNotElementType()
                    .Cast<Conduit>()
                    .ToList();

                foreach (var eletroduto in eletrodutos)
                {
                    // Verificar se o eletroduto está no caminho de um interruptor para luminárias
                    if (EstaNoCaminhoDeRetorno(eletroduto, out int quantidadeRetornos))
                    {
                        for (int i = 1; i <= 5; i++)
                        {
                            var paramRetorno = eletroduto.LookupParameter($"MRV_#{i}_Retorno");

                            if (paramRetorno != null && paramRetorno.AsInteger() == 0) // Apenas se ainda não estiver preenchido
                            {
                                paramRetorno.Set(quantidadeRetornos);
                                break; // Sai do loop após encontrar um slot disponível
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao preencher retornos: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Verifica se um eletroduto faz parte do caminho de retorno e retorna a quantidade de retornos necessários.
        /// </summary>
        private bool EstaNoCaminhoDeRetorno(Conduit eletroduto, out int quantidadeRetornos)
        {
            quantidadeRetornos = 0;

            // Encontrar interruptores conectados
            var interruptores = new FilteredElementCollector(_document)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.Family.Name.ToLower().Contains("Dispositivos de iluminação"))
                .ToList();

            foreach (var interruptor in interruptores)
            {
                var luminariasAssociadas = ObterLuminariasChaveadas(interruptor);
                if (luminariasAssociadas.Count > 0)
                {
                    foreach (var luminaria in luminariasAssociadas)
                    {
                        var caminhoEletrodutos = ObterCaminhoAteLuminaria(interruptor, luminaria);

                        if (caminhoEletrodutos.Contains(eletroduto.Id))
                        {
                            quantidadeRetornos += 1; // Incrementa um retorno para cada luminária encontrada
                        }
                    }
                }
            }

            return quantidadeRetornos > 0;
        }

        /// <summary>
        /// Retorna todas as luminárias associadas a um interruptor.
        /// </summary>
        private List<FamilyInstance> ObterLuminariasChaveadas(FamilyInstance interruptor)
        {
            var luminarias = new List<FamilyInstance>();

            foreach (Connector connector in interruptor.MEPModel?.ConnectorManager?.Connectors)
            {
                foreach (Connector conectado in connector.AllRefs)
                {
                    Element elemento = conectado.Owner;
                    if (elemento is FamilyInstance fi && fi.Category.Name.ToLower().Contains("luminária"))
                    {
                        luminarias.Add(fi);
                    }
                }
            }

            return luminarias;
        }


        private List<FamilyInstance> ObterInterruptoresAssociados(Document doc, string circuito)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.Family.Name.ToLower().Contains("interruptor"))
                .ToList();
        }

        private bool VerificarSeEhTrechoDeRetorno(Conduit eletroduto, string circuito)
        {
            var interruptores = ObterInterruptoresAssociados(_document, circuito);
            return interruptores.Any(interruptor =>
                interruptor.MEPModel?.ConnectorManager?.Connectors.Cast<Connector>()
                    .Any(c => c.AllRefs.Cast<Connector>().Any(refCon => refCon.Owner.Id == eletroduto.Id)) == true);
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RotaRetorno_Click(object sender, RoutedEventArgs e)
        {   
        }



        private void PreencherFase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var preencherFase = new PreencherFiacaoFase(_document);
                preencherFase.PreencherFase(_circuitosFiacao);

                MessageBox.Show("Preenchimento de Fase concluído!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao preencher Fase: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SelecionarLuminarias(UIDocument uiDocument)
        {
            Document doc = uiDocument.Document;

            // Buscar todas as luminárias no projeto
            var luminarias = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(fi => fi.Category != null && fi.Category.Name == "Luminárias") // Nome correto da categoria
                .ToList();

            // Se nenhuma luminária for encontrada, exibir uma mensagem
            if (luminarias.Count == 0)
            {
                MessageBox.Show("Nenhuma luminária encontrada!", "Depuração", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Criar uma lista para armazenar os IDs dos interruptores (Switch ID)
            List<string> switchIdList = new List<string>();

            foreach (var luminaria in luminarias)
            {
                // Obter o parâmetro SWITCH ID da luminária
                Parameter switchIdParam = luminaria.get_Parameter(BuiltInParameter.RBS_ELEC_SWITCH_ID_PARAM);

                if (switchIdParam != null)
                {
                    string switchId = switchIdParam.AsString();
                    if (!string.IsNullOrEmpty(switchId))
                    {
                        switchIdList.Add($"Luminária: {luminaria.Id.IntegerValue} - Switch ID: {switchId}");
                    }
                    else
                    {
                        switchIdList.Add($"Luminária: {luminaria.Id.IntegerValue} - Switch ID: [Vazio]");
                    }
                }
                else
                {
                    switchIdList.Add($"Luminária: {luminaria.Id.IntegerValue} - Switch ID: [Parâmetro não encontrado]");
                }
            }

            // Criar uma TaskDialog para exibir os Switch ID
            TaskDialog taskDialog = new TaskDialog("Switch IDs das Luminárias");
            taskDialog.MainInstruction = "Lista de Switch IDs das luminárias encontradas:";
            taskDialog.MainContent = string.Join("\n", switchIdList);
            taskDialog.Show();

            // Selecionar as luminárias na vista ativa
            uiDocument.Selection.SetElementIds(luminarias.Select(l => l.Id).ToList());
        }


        public void SelecionarInterruptores(UIDocument uiDocument)
        {
            Document doc = uiDocument.Document;

            // Buscar todos os interruptores no projeto
            var interruptores = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(fi => fi.Category != null && fi.Category.Name == "Dispositivos de iluminação") // Nome correto da categoria
                .ToList();

            // Se nenhum interruptor for encontrado, exibir uma mensagem
            if (interruptores.Count == 0)
            {
                MessageBox.Show("Nenhum interruptor encontrado!", "Depuração", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Criar uma lista para armazenar os IDs dos interruptores (Switch ID)
            List<string> switchIdList = new List<string>();

            foreach (var interruptor in interruptores)
            {
                // Obter o parâmetro SWITCH ID da luminária
                Parameter switchIdParam = interruptor.get_Parameter(BuiltInParameter.RBS_ELEC_SWITCH_ID_PARAM);

                if (switchIdParam != null)
                {
                    string switchId = switchIdParam.AsString();
                    if (!string.IsNullOrEmpty(switchId))
                    {
                        switchIdList.Add($"Interruptor: {interruptor.Id.IntegerValue} - Switch ID: {switchId}");
                    }
                    else
                    {
                        switchIdList.Add($"Interruptor: {interruptor.Id.IntegerValue} - Switch ID: [Vazio]");
                    }
                }
                else
                {
                    switchIdList.Add($"Interruptor: {interruptor.Id.IntegerValue} - Switch ID: [Parâmetro não encontrado]");
                }
            }

            // Criar uma TaskDialog para exibir os Switch ID
            TaskDialog taskDialog = new TaskDialog("Switch IDs dos interruptores");
            taskDialog.MainInstruction = "Lista de Switch IDs dos interruptores encontrados:";
            taskDialog.MainContent = string.Join("\n", switchIdList);
            taskDialog.Show();

            // Selecionar os interruptores na vista ativa
            uiDocument.Selection.SetElementIds(interruptores.Select(l => l.Id).ToList());
        }

        /// <summary>
        /// Encontra o interruptor correspondente ao Switch ID.
        /// </summary>
        private FamilyInstance EncontrarInterruptorPorSwitchID(Document doc, string switchID)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .FirstOrDefault(fi => fi.Category.Name == "Dispositivos de iluminação" &&
                                      fi.get_Parameter(BuiltInParameter.RBS_ELEC_SWITCH_ID_PARAM) != null &&
                                      fi.get_Parameter(BuiltInParameter.RBS_ELEC_SWITCH_ID_PARAM).AsString() == switchID);
        }

        /// <summary>
        /// Retorna a lista de eletrodutos que conectam um interruptor a uma luminária.
        /// </summary>
        private List<ElementId> ObterCaminhoAteLuminaria(FamilyInstance interruptor, FamilyInstance luminaria)
        {
            List<ElementId> caminho = new List<ElementId>();

            // 🔍 Se o interruptor estiver aninhado, subimos para a família principal
            if (interruptor != null && interruptor.SuperComponent != null)
            {
                interruptor = interruptor.SuperComponent as FamilyInstance;
            }

            if (interruptor == null || luminaria == null)
            {
                TaskDialog.Show("Erro", "Interruptor ou Luminária são nulos.");
                return caminho;
            }

            // 🚀 **Implementação de BFS (Menor Caminho)**
            Queue<List<ElementId>> fila = new Queue<List<ElementId>>();
            HashSet<ElementId> visitados = new HashSet<ElementId>();

            fila.Enqueue(new List<ElementId> { interruptor.Id });
            visitados.Add(interruptor.Id);

            while (fila.Count > 0)
            {
                var caminhoAtual = fila.Dequeue();
                var ultimoElementoId = caminhoAtual.Last();
                var ultimoElemento = _document.GetElement(ultimoElementoId);

                // Se encontrou a luminária, retorna o caminho
                if (ultimoElementoId == luminaria.Id)
                {
                    return caminhoAtual;
                }

                // Busca conectores ativos do último elemento analisado
                var conectores = ObterConnectoresAtivos(ultimoElemento);

                foreach (var connector in conectores)
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

            // Se nenhum caminho foi encontrado, exibir mensagem
            TaskDialog.Show("Erro", $"Nenhum caminho encontrado entre Interruptor {interruptor.Id} e Luminária {luminaria.Id}.");
            return new List<ElementId>();
        }

        private IEnumerable<Connector> ObterConnectoresAtivos(Element elemento)
        {
            var conectoresAtivos = new List<Connector>();

            if (elemento is FamilyInstance familyInstance)
            {
                if (familyInstance.SuperComponent != null)
                {
                    // Se for um componente aninhado, pega o SuperComponent (principal)
                    elemento = familyInstance.SuperComponent;
                }

                var connectorSet = (elemento as FamilyInstance)?.MEPModel?.ConnectorManager?.Connectors;
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





        private List<FamilyInstance> ObterLuminariasPorSwitchId(Document doc, string switchId)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(fi => fi.Category != null && fi.Category.Name == "Luminárias") // Nome correto da categoria
                .Where(fi =>
                {
                    Parameter param = fi.get_Parameter(BuiltInParameter.RBS_ELEC_SWITCH_ID_PARAM);
                    return param != null && param.AsString() == switchId;
                })
                .ToList();
        }





        /// <summary>
        /// Realiza a seleção dos elementos no Revit para destacar a rota.
        /// </summary>
        private void SelecionarElementosNaTela(UIDocument uiDocument, List<ElementId> elementos)
        {
            uiDocument.Selection.SetElementIds(elementos);
        }


        /// <summary>
        /// Obtém todos os interruptores do modelo.
        /// A categoria correta no Revit para interruptores é "Dispositivos de iluminação".
        /// </summary>
        private List<FamilyInstance> ObterTodosInterruptores()
        {
            if (_document == null)
                return new List<FamilyInstance>();

            return new FilteredElementCollector(_document)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Category != null && fi.Category.Name == "Dispositivos de iluminação")
                .ToList();
        }

        /// <summary>
        /// Obtém todas as luminárias associadas a um determinado interruptor.
        /// A categoria correta no Revit para luminárias é "Luminárias".
        /// </summary>
        private List<FamilyInstance> ObterLuminariasAssociadas(FamilyInstance interruptor)
        {
            if (interruptor == null || _document == null) return new List<FamilyInstance>();

            string switchId = interruptor.get_Parameter(BuiltInParameter.RBS_ELEC_SWITCH_ID_PARAM)?.AsString();
            if (string.IsNullOrEmpty(switchId)) return new List<FamilyInstance>();

            return new FilteredElementCollector(_document)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(fi => fi.Category.Name == "Luminárias" &&
                             fi.get_Parameter(BuiltInParameter.RBS_ELEC_SWITCH_ID_PARAM)?.AsString() == switchId)
                .ToList();
        }



        /// <summary>
        /// Encontra a rota entre um interruptor e uma luminária usando conectores.
        /// </summary>
        private List<ElementId> ObterCaminhoEntreElementos(FamilyInstance interruptor, FamilyInstance luminaria)
        {
            List<ElementId> caminho = new List<ElementId>();
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

            return new List<ElementId>(); // Retorna vazio se nenhum caminho for encontrado
        }


        /// <summary>
        /// Seleciona os elementos no Revit para destaque.
        /// </summary>
        private void SelecionarElementosNoRevit(Dictionary<ElementId, List<ElementId>> rotas)
        {
            List<ElementId> elementosSelecionar = rotas.SelectMany(r => r.Value).Distinct().ToList();
            _uiDocument.Selection.SetElementIds(elementosSelecionar);
        }


        private FamilyInstance ObterFamiliaPrincipal(FamilyInstance elemento)
        {
            if (elemento.SuperComponent != null)
            {
                // Retorna a família principal onde o interruptor está aninhado
                return elemento.SuperComponent as FamilyInstance;
            }
            return null;
        }


        public class FiacaoCircuito
        {
            public string Circuito { get; set; }
            public string BitolaFaseNeutro { get; set; }
            public string BitolaTerra { get; set; }
            public int QuantidadeFase { get; set; }
            public int QuantidadeNeutro { get; set; }
            public int QuantidadeTerra { get; set; }
            public object PreencherFiacaoRetorno { get; private set; }
        }
    }
}
