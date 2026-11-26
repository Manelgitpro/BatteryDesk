# Contrato de segurança do BatteryDesk

Este documento é uma regra obrigatória de arquitetura e revisão.

## Garantia de comportamento pretendida

BatteryDesk nunca deve alterar um periférico. Não implementar operações que gravem ou mudem configurações, firmware, memória, macros, iluminação, volume interno, modos, pareamento, reset ou estado persistente de qualquer dispositivo.

Nenhuma comunicação ativa com um periférico pode ocorrer sem ação explícita do usuário e, quando houver envio de um relatório de solicitação, confirmação específica antes do envio.

## Matriz de operações

| Operação | Regra |
|---|---|
| Enumerar dispositivos e ler descritores HID | Permitido após o usuário iniciar a varredura |
| Ler identificadores e capacidades HID | Permitido |
| Capturar relatórios HID de entrada | Somente após clicar em Capturar; leitura passiva apenas da coleção proprietária 0xF100 |
| Consultar bateria do Delux M800 Pro | Só após confirmação específica em cada varredura; solicitação de telemetria documentada |
| Ler propriedades Bluetooth publicadas pelo Windows | Permitido após o usuário iniciar a função |
| Output Reports para periféricos | Proibido |
| Feature Reports de configuração ou desconhecidos | Proibido |
| Escrita, reset, firmware, macros, iluminação, pareamento ou mudança de modo | Proibido |
| Captura, consulta ou varredura em segundo plano sem solicitação | Proibido |

## Exceção estreita: telemetria do Delux

O leitor do Delux M800 Pro usa o Feature Report 0C 01 20 exclusivamente para solicitar telemetria de bateria do receptor identificado por VID/PID 248A:5B2F e coleção HID FF00:0001. O envio exige explicitamente o argumento explicitlyAuthorizedByUser=true, valor que só pode ser passado após confirmação afirmativa exibida ao usuário em cada varredura.

Esse pedido não é um comando de configuração conhecido. Mesmo assim, por envolver envio de dados ao dispositivo, ele não roda sem consentimento explícito. A opção padrão da confirmação é Não.

## Monitoramento estimativo de carga do Zeus Pro

O timer de carga usa somente notificações de alteração da árvore de dispositivos do Windows e a enumeração HID de leitura para identificar a interface USB-C `H510-PRO USB Gaming Headset` (VID/PID 040B:0897). A enumeração abre interfaces com acesso desejado zero e consulta apenas identificadores/descritores; não lê relatórios e não envia pacotes ao headset. O timer grava apenas timestamps locais em arquivo de sessão. O receptor sem fio não deve iniciar o timer.

O tempo de aproximadamente 3 horas é uma referência publicada pela Redragon, não uma leitura de carga. O app nunca apresenta essa estimativa como percentual ou confirmação real de 100%. A função só monitora presença de cabo enquanto a janela do BatteryDesk está aberta; se o cabo for removido enquanto o app estiver fechado, a interface informa que não sabe o momento exato da remoção.
## Captura do Zeus Pro

A captura 040B:0897 abre somente a coleção proprietária 0xF100 com acesso GENERIC_READ e lê somente relatórios de entrada. Não abre as coleções comuns de multimídia ou telefonia. Não chama HidD_SetFeature, não emite Output Reports e não envia pacotes ao headset. Só começa após o usuário clicar no botão de captura.

## Revisão obrigatória para novas integrações

Antes de adicionar um driver:
1. Documentar VID/PID, coleção, report ID, direção e formato.
2. Separar leitura passiva de solicitação ativa.
3. Não implementar comandos desconhecidos ou operações de mutação.
4. Exigir confirmação por operação sempre que qualquer dado for enviado ao periférico.
5. Verificar por revisão de código que não existem caminhos automáticos de escrita.
6. Mostrar claramente na interface o que será lido/enviado, para qual dispositivo e com qual finalidade.

Se não for possível demonstrar que uma operação não altera o dispositivo, ela não deve ser implementada.

## Transparência

Nenhum programa consegue oferecer uma garantia matemática contra defeitos de firmware, drivers ou comportamento inesperado do próprio dispositivo. O compromisso do BatteryDesk é que o código do aplicativo não contenha nem execute funções destinadas a alterar periféricos; operações ativas são restritas, documentadas e exigem consentimento explícito.
