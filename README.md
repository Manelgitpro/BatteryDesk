# BatteryDesk v0.4.0

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

## Segurança e limites da varredura

- A varredura do teclado consulta descritores e capacidades HID; não envia relatórios Output ou Feature e não altera iluminação, macros, firmware ou configurações.
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

1. Validar a varredura de coleções e os metadados HID no teclado conectado.
2. Investigar protocolos de bateria de headsets pelo receptor USB de 2,4 GHz.
3. Implementar adaptadores adicionais para fabricantes e modelos documentados.
4. Adicionar histórico, notificações, bandeja do sistema e inicialização com o Windows.
