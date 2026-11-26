# BatteryDesk v0.3.0

Aplicativo desktop para Windows 10/11, desenvolvido em C#/.NET 10 e WPF, para identificar periféricos sem fio e consultar níveis reais de bateria quando seus protocolos permitem.

## O que está implementado

- Interface desktop em modo escuro.
- Descoberta local de interfaces USB/HID.
- Leitura de VID, PID, revisão, fabricante, produto e número de série quando disponibilizados.
- Agrupamento das interfaces HID do receptor Delux M800 Pro para evitar cartões duplicados.
- Consulta de bateria do **Delux M800 Pro pelo receptor USB de 2,4 GHz** (VID:PID `248A:5B2F`).
- Exibição de percentual e estado de carga quando o receptor responde com dados válidos.
- Consulta direcionada somente à coleção HID correspondente ao uso `FF00:0001` e ao receptor conhecido.
- Descoberta local; não usa rede nem instala drivers.

## Compatibilidade e limitações

- O leitor de bateria 2,4 GHz desta versão é específico para o receptor Delux M800 Pro.
- O protocolo usa Feature Report `0C` e valida o identificador e a estrutura da resposta antes de apresentar percentual.
- A consulta não altera configurações do mouse; é uma solicitação de estado/bateria ao receptor.
- Outros periféricos aparecem na descoberta HID, mas ainda não têm leitura de bateria implementada.
- Interfaces HID podem pertencer ao próprio computador ou ser coleções diferentes de um mesmo receptor.
- Encontrar um VID/PID não garante, por si só, suporte à bateria; modelos diferentes exigem protocolos próprios.

O BatteryDesk não deve apresentar percentuais fictícios como se fossem leituras reais.

## Executar no Windows

Requer Windows 10/11. O executável publicado pelo workflow é autocontido e não exige instalar o .NET.

## Compilar

O workflow **Build BatteryDesk for Windows** compila e publica uma versão autocontida para Windows x64 em cada push para `main`. O artefato `BatteryDesk-windows-x64` fica disponível na execução do GitHub Actions por 14 dias.

## Próximas etapas

1. Validar a leitura do Delux M800 Pro em diferentes níveis de bateria e durante o carregamento.
2. Investigar a leitura de bateria do headset Redragon Zeus Pro pelo receptor 2,4 GHz.
3. Implementar adaptadores adicionais para fabricantes e modelos documentados.
4. Adicionar histórico, notificações, bandeja do sistema e inicialização com o Windows.
