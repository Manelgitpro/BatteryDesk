# BatteryDesk v0.2.0

Aplicativo desktop para Windows 10/11, desenvolvido em C#/.NET 10 e WPF, com foco em identificar periféricos sem fio e, nas próximas etapas, monitorar níveis reais de bateria.

## O que está implementado nesta versão

- Interface desktop em modo escuro.
- Descoberta local de interfaces HID presentes no Windows.
- Leitura de VID, PID, revisão, fabricante, produto e número de série quando disponibilizados pelo dispositivo.
- Atualização manual da lista de dispositivos.
- Descoberta em modo somente leitura: não envia Output Reports nem Feature Reports, não instala drivers, não usa rede e não solicita elevação.

## Limitações conhecidas

- Ainda não consulta porcentagem de bateria.
- Bluetooth GATT / Battery Service (0x180F) ainda não foi implementado.
- A lista pode incluir interfaces internas do computador e múltiplas interfaces pertencentes ao mesmo periférico.
- Detectar uma interface HID não garante que o dispositivo exponha sua bateria ao Windows.
- Protocolos proprietários exigirão pesquisa, validação e adaptadores específicos por fabricante/modelo.

O BatteryDesk não deve exibir percentuais fictícios como se fossem leituras reais.

## Executar no Windows

Requer Windows 10/11 e .NET 10 SDK. Abra o PowerShell na pasta do projeto e execute:

```powershell
dotnet run --project .\BatteryDesk.App\BatteryDesk.App.csproj
```

## Gerar o executável

O workflow **Build BatteryDesk for Windows** compila e publica uma versão autocontida para Windows x64 em cada push para `main` e também pode ser iniciado manualmente pela aba Actions.

Quando o workflow terminar com sucesso, o artefato `BatteryDesk-windows-x64` ficará disponível para download na execução por 14 dias.

## Próximas etapas

1. Validar a identificação HID com dispositivos físicos.
2. Implementar leitura Bluetooth GATT do serviço padrão de bateria.
3. Investigar protocolos documentados dos fabricantes e criar adaptadores específicos.
4. Adicionar histórico, notificações e integração com a bandeja do sistema.
