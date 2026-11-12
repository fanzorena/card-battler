# Revisão de UX — Card Battler (protótipo v0.2.0)

Telas analisadas em 1920x1080: garagem, dossiê e batalha.
Capturas do "antes" em `screenshots/antes_garagem.png`, `screenshots/antes_dossie.png` e `screenshots/antes_batalha.png`.
Capturas do "depois" no fim deste documento.

Referências usadas (só como linguagem visual, sem copiar arte nem a cara de nenhum jogo):
- **Batalha por turnos 2D clássica:** inimigo em cima à direita, jogador embaixo à esquerda, barras de vida claras, caixa de texto narrando as ações.
- **Robôs montados por partes:** o robô na tela precisa mostrar a build de verdade; cada parte é legível e tem estado.
- **Deckbuilders e TCGs:** custo grande no canto, nome, tipo, palavras-chave destacadas, estado "não jogável".
- **Tom cyberpunk sério:** fundo escuro, acentos em neon por fabricante, tipografia limpa, nada cartunesco.

## Problemas encontrados (por prioridade)

### P0 — impedem entender a luta

1. **Não existe robô na tela.** A build aparece só como barras de texto. Isso quebra a fantasia central ("robô montado com partes") e não deixa ver de relance qual parte está quebrada ou exposta.
   **Correção:** robô 2D montado proceduralmente com as partes equipadas. A silhueta varia por parte e a cor por fabricante, com estados visuais: dano, avariada, chassi exposto, blindagem, Calor e status.
2. **Sem feedback de ação.** Dano, blindagem absorvida e parte quebrada só aparecem no log, que é uma lista corrida de 10 linhas. A ação do inimigo acontece toda de uma vez, sem ritmo.
   **Correção:**
   - números flutuantes sobre a parte atingida;
   - flash e tremida no golpe, efeito de quebra e escudo absorvendo;
   - aviso de turno ("SEU TURNO" / "TURNO DO INIMIGO");
   - ações do inimigo mostradas em sequência;
   - caixa de texto com as últimas ações.
3. **A intenção do inimigo é um parágrafo de texto** longe do inimigo ("Chute: 5 em parte aleatória..."). Exige leitura e não mostra o alvo no robô.
   **Correção:** ícones em fichas acima do inimigo. Cada ficha mostra o tipo (ataque, bloqueio, sucata, status), o valor e o alvo ("Braço Esq" ou "?").
4. **Cartas são botões cinza** com o custo em texto pequeno ao lado do nome. Tudo tem o mesmo peso: custo, origem, alvo e efeito. A sucata é um botão separado e não deixa claro que gasta a carta.
   **Correção:**
   - moldura de carta com o custo grande em círculo no canto superior esquerdo;
   - nome, ícone da parte de origem e selo de alvo ("ESCOLHE A PARTE" / "PARTE ALEATÓRIA");
   - texto com palavras-chave destacadas e cor do fabricante;
   - estado "não jogável" escurecido;
   - sucata como aba presa à carta.

### P1 — atrito e legibilidade

5. **Hierarquia fraca.** Nome do robô, números de status e barras têm todos o mesmo tamanho e a mesma cor. Blindagem, Calor e Esquiva aparecem como texto mesmo valendo 0.
   **Correção:** status com ícone e cor próprios, mostrados só quando relevantes. A vida do chassi fica em destaque.
6. **O chassi protegido ou exposto é só uma palavra** dentro da barra.
   **Correção:** um anel em volta do núcleo do robô, apagado quando protegido e laranja pulsando quando exposto, mais um texto curto.
7. **Escolher o alvo é pouco claro.** A barra fica amarela, mas não aparece cursor nem dica, e o robô não reage.
   **Correção:** as partes válidas do robô inimigo brilham e mostram o nome, as inválidas ficam apagadas, e aparece a dica "Escolha uma parte" com um botão Cancelar.
8. **Palavras-chave sem explicação** (Encadear, Blindagem, Calor, Sobrecarga, Corrosão, Impacto, Sucata).
   **Correção:** tooltip ao passar o mouse na carta, com a explicação das palavras-chave que ela usa.
9. **Combo invisível.** Nada avisa que uma carta vai encadear.
   **Correção:** brilho de Encadear na carta quando ela encadeia com a última jogada.
10. **Garagem em formato de formulário.** Lista de linhas com botões "<" e ">". O robô e o peso não aparecem visualmente, e o deck é uma lista de texto.
    **Correção:**
    - robô no centro com os slots em volta, no lugar de cada parte;
    - barra de peso;
    - selos de bônus de fabricante;
    - deck como mini-cartas.
11. **O dossiê parece um formulário.**
    **Correção:** apresentar como um arquivo do mundo do jogo (cabeçalho "DOSSIÊ", carimbo de categoria, rumores como citações).

### P2 — acabamento

12. **Fonte pequena para 1080p** (corpo de cerca de 14 px) e fundo totalmente liso.
    **Correção:** escala de referência em 1920x1080, corpo de 22–26 px e títulos de 30+ px; fundo com gradiente escuro e grade sutil.
13. **Animações de compra e descarte** ausentes.
    **Correção:** as cartas entram deslizando a partir do deck e saem para o descarte; a carta jogada voa até o alvo.
14. **Acessibilidade:** a cor não pode ser o único sinal.
    **Correção:** estados sempre com texto ou forma além da cor (avariada = cinza + "X" + texto; exposto = anel + texto), e contraste alto no texto.
15. **Fluxo entre telas:** "Desistir (garagem)" fica no canto e é fácil de clicar sem querer.
    **Correção:** botão menor e discreto, com confirmação.

## Escolha de tecnologia: uGUI (Canvas) montado por código

- A batalha é um HUD de jogo cheio de elementos animados e gerados na hora: partes do robô, números flutuantes, cartas voando, tremidas. O uGUI (RectTransform + Canvas) é direto para isso, com corrotinas e transformações em qualquer elemento.
- O EventSystem do uGUI dá hover, clique e arrastar sem código extra, e funciona com o Input System do projeto.
- Tudo é criado por código a partir de um `Theme` central (paleta, cores por fabricante e elemento, tamanhos de fonte). Não há prefabs nem cenas para editar à mão, e o protótipo continua subindo em qualquer cena.
- O UI Toolkit seria a escolha para telas de menu e formulários (USS e layout flex). Mas animação por elemento e desenho customizado (o robô) ainda são mais trabalhosos nele em runtime. Fica como opção para menus no futuro.
- Texto: fonte embutida do Unity (`LegacyRuntime.ttf`), para não depender de importar recursos do TextMeshPro neste protótipo. Trocar para TextMeshPro com uma fonte própria fica pendente.

## Depois (implementado nesta rodada)

Capturas em 1920x1080:
- `screenshots/depois_garagem.png`
- `screenshots/depois_dossie.png`
- `screenshots/depois_batalha_inicio.png`
- `screenshots/depois_batalha.png`: modo de escolher alvo.

| # | Problema | Situação |
|---|---|---|
| 1 | Robô na tela | **Feito.** Robô 2D procedural com as 22 partes. Cada parte tem silhueta própria: lente e antena, viseira, elmo com crista, coroa; carcaça remendada, placa com faixas, chassi afinado, tórax com aletas; punho, serra circular, canhão, escudo, lâmina, bobinas, seringa, lança-chamas; rodas, esteiras, pernas digitígradas, hover. A cor vem do fabricante e o núcleo tem anel de chassi. Estados: rachaduras e escurecimento com dano; cinza com X e faíscas quando avariada; anel laranja com o chassi exposto; escudo azul com o valor da blindagem; brilho de Calor; selos de Calor, Sobrecarga, Esquiva, Mira e Finta; selo de Corrosão e Ponto Fraco na parte. O inimigo aparece espelhado. |
| 2 | Feedback de ação | **Feito.** A batalha reproduz os eventos do log em sequência: números flutuantes (vermelho nas partes, laranja no chassi), flash e tremida no golpe, "-N BLINDAGEM", "ESQUIVOU", "ENCADEAR!", explosão de faíscas e "AVARIADA!", "CHASSI EXPOSTO", "+N BLINDAGEM", reparo em verde, aviso de turno e de fase do chefe, e caixa de texto com as últimas ações. O turno do inimigo acontece passo a passo. |
| 3 | Intenção como texto | **Feito.** Fichas com ícone acima do inimigo: ataque "9 → Braço Dir" ou "5 → ?" para aleatório, bloqueio "+6", sucata, mira e status. |
| 4 | Cartas como botões | **Feito.** Moldura com a cor do fabricante, custo grande (em vermelho quando a parte está avariada), ícone do tipo com o valor principal, parte de origem, selo "ESCOLHE A PARTE" / "PARTE ALEATÓRIA", texto com palavras-chave coloridas, estado não jogável e brilho de Encadear. Ao passar o mouse a carta sobe e mostra o tooltip; jogar é com clique. A carta voa até o alvo ao ser jogada e até a energia ao ser sucateada. Na compra, as cartas saem do deck; ao encerrar o turno, vão para o descarte. A sucata é uma aba presa à carta ("SUCATA: +1 ENERGIA" / "SUCATA: COMPRA 1"). |
| 5 | Hierarquia | **Feito.** Painéis com nome, categoria colorida, barra de chassi em destaque (protegido/exposto) e barras por parte na cor do fabricante, com "AVARIADA". Status só aparecem quando relevantes. |
| 6 | Chassi protegido/exposto | **Feito.** Anel no núcleo do robô e barra laranja "EXPOSTO". |
| 7 | Escolha de alvo | **Feito.** As partes válidas brilham, o chassi só fica clicável quando exposto, e aparecem a dica e o botão Cancelar. |
| 8 | Palavras-chave | **Feito.** Tooltip com a explicação das palavras-chave e da regra de alvo. |
| 9 | Combo invisível | **Feito.** Brilho de Encadear quando a carta encadeia com a última jogada. |
| 10 | Garagem em formulário | **Feito.** Robô no centro com os slots em volta, cada um com a cor do fabricante, selo, integridade, peso, elemento e cartas. Barra de peso (vermelha acima da carga), selos de bônus e deck em mini-cartas. |
| 11 | Dossiê | **Feito.** "Arquivo" com cabeçalho, carimbo da categoria, rumores em itálico, "Funciona bem" e "Evite" com palavras-chave coloridas. |
| 12 | Fonte e fundo | **Feito.** Escala de referência 1920x1080 (a mesma tela em 1280x720), texto de 18–26 px e títulos de 30–110 px. Fundo escuro com brilho violeta e grade sutil. |
| 13 | Compra e descarte | **Feito** (ver item 4). |
| 14 | Acessibilidade | **Feito em parte.** Todo estado tem texto ou forma além da cor (X, "AVARIADA", "EXPOSTO", "?" no alvo aleatório). Ainda falta: modo daltônico e escala de fonte configurável. |
| 15 | Desistir | **Feito.** Botão discreto que pede um segundo clique para confirmar. |

**Adiado:**
- arrastar a carta até o alvo (hoje é clique);
- fonte própria em TextMeshPro;
- ícones desenhados para os elementos (hoje são cores e formas simples);
- partículas e shaders;
- som;
- tela de opções.

**Técnico:**
- UI em uGUI montada por código. O `Theme` concentra paleta, sprites procedurais e construtores.
- `RobotView`, `CardView` e `Fx` são os blocos de tela e efeitos; `GameUI`, `GarageScreen` e `BattleScreen` são as telas.
- As regras não mudaram. Os testes de regras e o simulador continuam iguais.
- Novos testes de UI cobrem o destaque de palavras-chave, o tipo das cartas, as fichas de intenção (alvo aleatório "?" e alvo escolhido) e o robô montado com as 22 partes e seus estados.

## Segundo passe — ícones de parte e área de sucata

Pedido do dono: trocar os nomes das partes (Braço Esq, Braço Dir...) por ícones, colocar os ícones também na interface para o jogador ligar carta ↔ parte, e trocar o botão de sucata em cada carta por uma área dedicada onde a carta é arrastada.

Capturas em `screenshots/`:
- `depois2_garagem.png`
- `depois2_batalha.png`
- `depois2_batalha_720p.png`
- `depois2_arrastando.png` (carta sendo arrastada para a sucata)

**Ícones de parte**
- Cada ícone é um mini-robô com a parte em destaque: Cabeça, Torso, Braço esquerdo, Braço direito, Pernas, Núcleo, Chassi e Frame (este para a carta assinatura).
- Os braços têm uma marca **E** / **D**, então esquerdo e direito nunca dependem só da cor.
- O ícone fica na cor do fabricante, com anel e parte destacada maior, e continua legível em 1280x720.
- Ao passar o mouse aparece o nome completo, para acessibilidade.
- O mesmo ícone aparece:
  - no canto da carta, no lugar do texto da origem;
  - nas barras de integridade dos painéis, no lugar do nome do slot;
  - **no próprio robô**, ao lado de cada parte;
  - na intenção do inimigo ("6 → [ícone]"; aleatório continua "?");
  - na caixa de texto, no início da linha, com o nome da parte removido do texto;
  - ao lado do número de dano;
  - nos slots da garagem e nas mini-cartas do deck.
- O inimigo não é mais espelhado: o braço esquerdo está sempre à esquerda, como no ícone.

**Área de sucata e arrastar cartas**
- A aba de sucata das cartas saiu. Agora há uma lixeira "SUCATA" ao lado da energia, que mostra o efeito do momento: "+1 ENERGIA", ou "COMPRA 1 CARTA" na primeira sucata do Vira-lata, mais "+N BLINDAGEM" com o bônus Ferrovelho.
- Arrastar uma carta:
  - soltar na **sucata** sucateia;
  - soltar numa **parte válida do inimigo** (ou numa parte sua, para reparo) joga a carta naquele alvo; as partes válidas brilham durante o arraste;
  - soltar **acima da mão** joga a carta, ou abre a escolha de alvo se a carta escolhe a parte;
  - soltar em qualquer outro lugar cancela e a carta volta para a mão.
- O clique continua funcionando como alternativa.
- A sucata destaca e cresce quando a carta passa por cima. A carta voa para a lixeira e aparece "+1" ou "+1 CARTA" na energia.

**Testes de UI novos:**
- ícones distintos, com E/D nos braços;
- decisão do que acontece ao soltar a carta (sucata, parte, área de jogo, cancelar);
- troca dos nomes de parte pelos ícones na caixa de texto, sem estragar nomes de carta como "Cabeça Dura".

O arrastar até a sucata também foi verificado de ponta a ponta no Play mode.

## Terceiro passe — garagem separada da luta, ficha do robô e "?"

Pedido do dono: separar a garagem da luta, com um botão Continuar que leva à seleção de adversário; no lugar do dossiê, a ficha de estatísticas do robô; e um "?" ao lado de cada item, que explica a característica ao passar o mouse.

Capturas:
- `screenshots/fluxo_garagem.png` (com o tooltip do "?" de Chassi aberto)
- `screenshots/fluxo_garagem_comparando.png` (comparando outra peça)
- `screenshots/fluxo_adversario.png`

**Fluxo:** Garagem → **CONTINUAR** → Adversário → **LUTAR** → Batalha. No fim da luta há três botões: Revanche, Outro adversário e Voltar à garagem. "Desistir" volta para a seleção de adversário.

**Garagem**
- Serve só para montar o robô: frame, peças, Robô 1–3 e deck. O adversário e o dossiê saíram de lá.
- **CONTINUAR** fica desligado quando a build é inválida (por exemplo, acima da carga), e o motivo aparece em vermelho.

**Ficha do robô** (no lugar do dossiê)
- Linhas: chassi, integridade total e de cada parte (com o ícone), energia por turno, calor por turno, tamanho da mão, peso / carga, bônus de fabricante, passiva do frame, elementos e status, composição do deck (ataque / defesa / utilidade), custo médio e alvo dos ataques (quantos escolhem a parte e quantos são aleatórios).
- Os valores se atualizam na hora.
- Ao passar o mouse em **<** ou **>** de um slot, a ficha mostra a peça daquele botão com a diferença de cada valor: verde se melhora, vermelho se piora, "muda" nos textos.

**"?" em cada item**
- Cada "?" mostra uma explicação curta, escrita para as regras atuais.
- Os textos ficam num **glossário** em `content.json`, a mesma fonte que os tooltips de palavras-chave das cartas já usam. Assim dá para reaproveitar na batalha.

**Seleção de adversário**
- Colunas por categoria (Fácil, Moderado, Difícil, Chefe), cada adversário com seu robô.
- O dossiê do selecionado aparece como arquivo.
- Uma prévia do seu robô mostra os números principais.

**Testes novos:**
- ficha do robô inicial;
- glossário cobrindo todas as linhas da ficha e todas as palavras-chave;
- CONTINUAR bloqueado para build acima da carga;
- navegação garagem → adversário → batalha → garagem, incluindo o bloqueio quando a build é inválida.
