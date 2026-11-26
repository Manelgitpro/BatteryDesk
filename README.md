# BatteryDesk v0.6.0

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
- Cronômetro estimativo de carga do Zeus Pro: inicia quando o Windows detecta a interface USB-C `H510-PRO USB Gaming Headset`, mostra contagem regressiva baseada em aproximadamente 3 horas e encerra quando o cabo é removido.
- Persistência local do início da sessão para manter a contagem após reiniciar o BatteryDesk, com indicação explícita quando não foi possível observar a remoção do cabo.
- Gravação dos relatórios HID de entrada recebidos em arquivo local para análise do protocolo.

## Cronômetro estimativo de carga do Zeus Pro

A ficha técnica internacional publicada pela Redragon informa carregamento em aproximadamente 3 horas e autonomia de até 18 horas. A página brasileira oficial confirma o modelo H510-PRO e o cabo USB-C, mas não publica a capacidade em mAh. Por isso, o BatteryDesk usa apenas a referência temporal aproximada de 3 horas; não assume capacidade de bateria nem apresenta uma porcentagem real.

O timer:
- detecta a presença da interface HID `H510-PRO USB Gaming Headset` (VID:PID `040B:0897`), diferenciando-a do receptor sem fio pelo nome do produto;
- inicia a contagem quando a interface USB-C é conectada e encerra quando é removida;
- exibe a contagem regressiva até a referência de 3 horas e avisa quando esse tempo é alcançado;
- possui botão para zerar o ciclo; com o cabo conectado, reinicia a contagem a partir do momento do clique;
- persiste os horários localmente em `%LOCALAPPDATA%\BatteryDesk\ZeusProChargeSession.json`;
- somente observa eventos de conexão do Windows e enumera descritores HID com acesso de leitura; não lê relatórios de entrada/saída nem envia comandos ao headset.

O tempo de 3 horas é uma estimativa anunciada pela marca, não uma confirmação de 100%. Se o BatteryDesk for fechado enquanto o cabo for removido, o instante exato da remoção não pode ser recuperado; a interface sinaliza a sessão como interrompida e pede para zerar o ciclo.
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

1. Validar no computador que a detecção da interface USB-C inicia e encerra o cronômetro do H510-PRO corretamente.
2. Refinar a identificação do dispositivo caso o Windows apresente uma variação no nome da interface.
3. Continuar a investigação de protocolos reais de bateria somente quando houver documentação suficiente e sem enviar comandos desconhecidos.
4. Implementar adaptadores adicionais para fabricantes e modelos documentados.
