# BatteryDesk v0.5.1

Aplicativo desktop para Windows 10/11, desenvolvido em C#/.NET 10 e WPF, para identificar periféricos e consultar níveis reais de bateria quando seus protocolos permitem.

## O que está implementado

- Interface desktop em modo escuro.
- Descoberta local de interfaces USB/HID.
- Leitura de VID, PID, revisão, fabricante, produto e número de série quando disponibilizados.
- Leitura de capacidades das coleções HID: Usage Page, Usage e tamanhos dos relatórios Input, Output e Feature.
- Identificação de coleções de teclado, mouse, controles multimídia e coleções específicas de fabricantes.
- Agrupamento das coleções HID do teclado SEMICO USB Gaming Keyboard (1A2C:9605) por instância física inferida do caminho HID.
- Detalhamento de interface MI e coleção COL quando estes índices estão presentes no caminho do dispositivo.
- Agrupamento das interfaces do receptor Delux M800 Pro para evitar cartões duplicados.
- Consulta de bateria do **Delux M800 Pro pelo receptor USB de 2,4 GHz** (VID:PID `248A:5B2F`).
- Exibição de percentual e estado de carga quando o receptor responde com dados válidos.
- **Captura HID passiva do Redragon Zeus Pro H510-PRO** (VID:PID `040B:0897`), limitada exclusivamente à coleção proprietária do fabricante `F100`.
- Gravação dos relatórios HID de entrada recebidos em arquivo local para análise do protocolo.

## Investigação do Redragon Zeus Pro H510-PRO

A captura passiva pode ser iniciada após uma varredura que encontre a coleção proprietária HID `0xF100`. No primeiro teste, deixe o headset em repouso durante os 30 segundos, sem pressionar botões: queremos registrar somente o que ele transmite espontaneamente.

Os arquivos são salvos em:

`%LOCALAPPDATA%\BatteryDesk\Captures`

A captura:
- abre somente a interface HID proprietária `0xF100` do dispositivo `040B:0897`;
- lê relatórios de entrada com `ReadFile`; não abre as coleções de teclas multimídia ou telefonia;
- não chama `HidD_SetFeature`, não envia Output Reports e não tenta comandos desconhecidos;
- registra página de uso, uso, horário UTC e bytes recebidos;
- não registra caminho HID nem número de série no arquivo.

A captura de entrada é uma primeira etapa de descoberta. Ela não observa, por si só, todos os transfers USB de saída/controle e não garante que a bateria seja transmitida espontaneamente. Se não houver relatórios, o próximo passo de baixo risco é uma captura USBPcap/Wireshark durante as ações físicas do headset. Como o site oficial brasileiro informa que o H510-PRO não possui software próprio, não há atualmente um aplicativo oficial conhecido para gerar e capturar uma consulta de bateria.

## Contrato de segurança do projeto (obrigatório)

O BatteryDesk nunca deve executar operações destinadas a alterar um periférico. É proibido implementar gravação ou mudança de configurações, firmware, memória, macros, iluminação, volume interno, modos, pareamento ou estado persistente.

### Operações permitidas
- Enumerar dispositivos e ler descritores, identificadores e capacidades HID quando o usuário inicia a varredura.
- Ler relatórios HID de entrada passivamente, somente após ação explícita do usuário.
- Ler informações de telemetria publicadas pelo Windows.
- Enviar a solicitação de telemetria de bateria documentada do Delux M800 Pro somente após uma confirmação explícita em cada varredura. Se o usuário negar, nenhum pacote é enviado.

### Operações proibidas
- Alterar configurações, perfis, macros, iluminação, volume interno, modos, memória ou pareamento do periférico.
- Gravar ou atualizar firmware, executar reset ou mudar modos de conexão.
- Enviar Output Reports, comandos de configuração ou Feature Reports desconhecidos.
- Executar captura, leitura ativa ou varredura automaticamente/em segundo plano, sem ação e consentimento explícitos.
- Interpretar tráfego desconhecido como autorização para enviar comandos.

Toda integração nova deve documentar VID/PID, coleção, report ID, direção, formato e finalidade. Qualquer comunicação ativa exige confirmação clara, específica e com opção negativa como padrão. Nenhuma operação que altere o periférico pode ser adicionada, mesmo com confirmação.

## Segurança e limites da varredura

- A varredura do teclado consulta descritores e capacidades HID; não envia relatórios Output ou Feature e não altera iluminação, macros, firmware ou configurações.
- A captura do Zeus Pro é somente leitura de relatórios HID de entrada. Não envia comandos ao dispositivo.
- Os tamanhos de relatório são metadados dos descritores, não conteúdo de teclas digitadas. O aplicativo não registra digitação.
- A classificação é baseada em Usage Page / Usage informados pelo dispositivo e pode não identificar todas as funções proprietárias.
- O teclado SEMICO aparece sem monitoramento de bateria nesta versão; a varredura é de diagnóstico, não uma função de bateria.
- O leitor de bateria 2,4 GHz continua específico para o receptor Delux M800 Pro.
- Encontrar um VID/PID ou uma coleção HID não garante, por si só, acesso à bateria; outros modelos exigem protocolos próprios.

O BatteryDesk não deve apresentar percentuais fictícios como se fossem leituras reais.

## Executar no Windows

Requer Windows 10/11. O executável publicado pelo workflow é autocontido e não exige instalar o .NET.

## Compilar

O workflow **Build BatteryDesk for Windows** compila e publica uma versão autocontida para Windows x64 em cada push para `main`. O artefato `BatteryDesk-windows-x64` fica disponível na execução do GitHub Actions por 14 dias.

## Próximas etapas

1. Analisar os relatórios HID recebidos durante ações físicas no H510-PRO.
2. Se o canal HID não trouxer dados, comparar uma captura USBPcap para inspecionar também transferências de controle/saída.
3. Validar qualquer estrutura candidata com mais de um estado conhecido de bateria, sem apresentar inferências como leitura real.
4. Implementar adaptadores adicionais para fabricantes e modelos documentados.
