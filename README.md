# BatteryDesk

Aplicativo desktop para Windows 10/11 que busca monitorar a bateria de periféricos sem fio, como headsets, mouses, teclados e controles.

## Objetivos

- Detectar dispositivos conectados por USB/HID e Bluetooth.
- Exibir informações reais de bateria quando o dispositivo/protocolo permitir.
- Manter histórico de níveis e oferecer notificações.
- Disponibilizar interface escura, bandeja do sistema e inicialização com o Windows.
- Informar claramente quando o nível de bateria não estiver disponível.

> **Compatibilidade honesta:** detectar um dispositivo HID não significa que seja possível consultar sua bateria. Muitos fabricantes usam protocolos proprietários. O BatteryDesk não deve inventar percentuais nem enviar comandos de escrita ao dispositivo durante a descoberta.

## Estado atual

Esta é a estrutura inicial do repositório. A implementação de leitura de bateria, suporte Bluetooth/GATT, histórico e notificações será adicionada e validada incrementalmente.

## Requisitos de desenvolvimento

- .NET 10 SDK
- Windows 10 ou Windows 11
- Para executar localmente: `dotnet run --project .\src\BatteryDesk.App\BatteryDesk.App.csproj`

## Compilação

O workflow do GitHub Actions compila e publica uma versão Windows x64 autocontida. O executável será disponibilizado como artefato da execução quando o build terminar com sucesso.

## Privacidade e segurança

A descoberta inicial é somente leitura. O aplicativo não coleta nem envia informações pessoais ou dados dos dispositivos para servidores externos.
