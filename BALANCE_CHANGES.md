# Mudanças de balanceamento

Valores que mudaram em relação ao documento de design / versão anterior, para atualizar o documento.
Medido com o simulador (Card Battler > Simulador), 500 lutas por confronto, jogador greedy.

## 2026-11-12 — inimigos do Distrito Neon (`feature/distrito-neon`)

Todos lutam na chuva (Elétrico +2). Ajuste para a meta:

| Inimigo | Categoria | Chassi extra | Cartas/turno | Energia extra | Vitória greedy |
|---|---|---|---|---|---|
| Drifter da Laje | Moderado | 10 | 4 | 0 | 67.5% |
| Muralha de Neon | Difícil | 6 | 4 | 2 | 55.2% |
| Kiro Sato (revanche) | Chefe | 8 | 4 | 0 | 59.4% |

Categorias (média): Fácil 99.8%, Moderado 67.9%, Difícil 56.6%, Chefe 57.6%.

## 2026-11-12 — terrenos (`feature/terrain`)

### Regras novas (código)

| Terreno | Arena | Regra |
|---|---|---|
| Piso de sucata | Fossos do porto (trabalho, ranking, maioria dos inimigos) | pernas de rodas esquivam só metade (o pedido era "perdem a esquiva"; ver abaixo) |
| Telhado na chuva | Distrito Neon (casa do Kiro) | ataques Elétricos +2 no primeiro golpe |
| Campo de supressão | Torre-Arena Kagemori (casa da Lâmina Corporativa) | Calor recebido de ataques dobrado |

Pernas Hover ignoram o terreno. O simulador agora luta na arena de casa de cada inimigo.

**Por que metade e não zero:** com "rodas não esquivam", todas as categorias caíram 20+ pontos (Chefe 37%, trabalho da Celeste 2%), porque a build inicial e 4 das 6 builds de referência usam rodas e todas as lutas da carreira são no Fosso. Pela metade, a queda fica entre 6 e 10 pontos, e dá para corrigir no chassi dos inimigos.

| Inimigo | Chassi extra antes | Depois |
|---|---|---|
| Bastião de Patrulha | 0 | −3 |
| Lâmina Corporativa | 8 | 10 |
| Kiro Sato | 12 | 10 |
| Rainha do Ácido | 8 | 6 |
| Mandíbula | 2 | −1 |

| Categoria | Meta | Antes (elementos) | Terreno, rodas sem esquiva | Rodas pela metade | Ajustado |
|---|---|---|---|---|---|
| Fácil | > 80% | 99.9% | 99.5% | 99.8% | 99.8% |
| Moderado | 65–75% | 71.9% | 59.8% | 66.3% | 68% |
| Difícil | 55–60% | 59.7% | 48.3% | 52.7% | 57.3% |
| Chefe | 55–60% | 59.5% | 37% | 49.3% | 55.8% |

Trabalho da Celeste (bot com a build inicial, que nunca troca de peça): 37% → 18%.

## 2026-11-12 — elementos automáticos (`feature/elements`)

### Regras novas (código)

| Regra | Valor |
|---|---|
| Elemento do ataque | o da peça de origem da carta (carta assinatura do frame não tem elemento) |
| Cinético | Impacto +2 no primeiro golpe contra parte avariada ou chassi exposto |
| Térmico / Elétrico / Químico | +1 Calor / Sobrecarga 1 turno / +1 Corrosão na parte, quando o ataque causa dano; não acumula com o mesmo status da carta |
| Resistência / fraqueza | −1 / +1 de dano por golpe na parte (chassi não tem) |

### Dados

| Peça | Resiste | Fraca a |
|---|---|---|
| Placa M2, Esteiras | Cinético | Elétrico |
| Elmo Bastion | — | Elétrico |
| Escudo de Braço | Cinético | — |
| Sensor K-7 | Elétrico | Cinético |
| Chassi Aero, Pulso Tesla | Elétrico | — |
| Coroa Nyx | Químico | Térmico |
| Injetor | Químico | — |
| Tórax Refrigerado | Térmico | Químico |
| Lança-chamas | Térmico | — |
| Carcaça Remendada | — | Químico |

| Inimigo | Chassi extra antes | Depois |
|---|---|---|
| Kiro Sato | 10 | 12 |
| Rainha do Ácido | 6 | 8 |
| Mandíbula | 4 | 2 |

### Resultado (greedy, média das builds)

| Categoria | Meta | Antes (v0.3.0) | Só elementos | Depois do ajuste |
|---|---|---|---|---|
| Fácil | > 80% | 99.9% | 99.9% | 99.9% |
| Moderado | 65–75% | 68.6% | 71.6% | 71.9% |
| Difícil | 55–60% | 56.5% | 64.9% | 59.7% |
| Chefe | 55–60% | 56% | 52.5% | 59.5% |

Trabalho da Celeste (bot, 500 tentativas): 30.2% → 37% de conclusão.

## 2026-11-11 — mira por custo e chassi protegido (`feature/targeting-rules`)

Motivo: no teste do dono, a luta era só "torso e depois chassi", as outras partes quase não levavam dano,
e comprar cartas de graça deixava fácil cavar a carta desejada.

### Regras novas (código)

| Regra | Antes | Depois |
|---|---|---|
| Alvo dos ataques | sempre o torso (Mirar escolhia a parte) | **custo 1: parte aleatória** (entre as partes inteiras); **custo 2–3: escolhe a parte**. Mirar faz o próximo ataque escolher, mesmo de custo 1. Vale o **custo base** da carta: a parte avariada (+1) não muda a regra. |
| Chassi | recebia o dano que passava do torso | só pode ser **escolhido como alvo** depois que alguma parte for destruída. Ataque aleatório nunca cai no chassi. |
| Derrota | torso e chassi em 0 | **chassi em 0**. Torso destruído vira só uma parte avariada (cartas +1, sem bônus). |
| Dano que sobra numa parte | ia para o torso / chassi | vai para o chassi **só se ele estiver exposto** (incluindo quando é essa parte que acabou de quebrar); senão se perde. Testamos "sempre perder": as lutas ficaram longas demais (mediana de 14–19 turnos). |
| Impacto (Encadear do Gancho) | +4 contra parte avariada | +4 contra parte avariada **ou chassi exposto** |
| Ponto Fraco | +3 no primeiro golpe de cada ataque | **+3 em todos os golpes** na parte neste turno |
| Sucata do Vira-lata (passiva) | primeira sucata do turno: +1 energia e compra 1 | primeira sucata do turno **compra 1 em vez de dar energia** |
| Fases do chefe | por torso + chassi | **por chassi** |

### Cartas

| Carta | Antes | Depois | Por quê |
|---|---|---|---|
| Varredura | 0: compre 1 | **1: compre 2** | Comprar carta passa a custar energia. |
| Arrancada | 0: compre 1 | **1: compre 2** | Idem. |
| Análise | 0: compre 1 e veja a próxima mão | **1: compre 2 e veja a próxima mão** | Idem. |
| Desmontar | 7, ignora blindagem; se avariar, compre 2 | se avariar, **compre 1** | Com a regra nova ela escolhe a parte e quebra peças com facilidade. |
| Investida | dano = toda a blindagem | **3/4 da blindagem** | Bastião ficava acima da meta. |
| Ácido | 3 de dano, 2 de Corrosão | **4 de dano, 3 de Corrosão** | Nyx ficou fraco: quase todas as cartas dele são de custo 1 (aleatórias). |
| Tóxico | custo 2 | **custo 1, escolhe a parte** | Precisa acertar a parte já corroída. |
| Deslizar | 3 de blindagem | **4 de blindagem** | Nyx. |
| Mordida do Campeão | (custo 0) | **escolhe a parte** | O golpe assinatura do chefe não pode ser aleatório. |

### Partes e frames

| O quê | Antes | Depois | Por quê |
|---|---|---|---|
| Coroa Nyx / Tórax Refrigerado / Injetor / Lança-chamas / Hover | integridade 4 / 13 / 6 / 7 / 9 | **6 / 15 / 8 / 9 / 9** (Hover 7 → 9) | Nyx abria o chassi rápido demais. |
| Chassi dos frames | Vira-lata 10, Duelista 6, Bastião 16 | **8, 5, 13** | O chassi virou a vida real; encurta as lutas. |

### Inimigos

| Inimigo | Antes | Depois |
|---|---|---|
| Rato de Laboratório | sem estratégia | **defensivo** (continua Fácil, ~100%) |
| Bastião de Patrulha | chassi +4 | chassi +0 |
| Lâmina Corporativa | chassi +22 | chassi +8 |
| Kiro Sato | chassi +13 | chassi +10 |
| Rainha do Ácido | chassi +34 | chassi +6 |
| Mandíbula (chefe) | frame Bastião, 5 estratégias (com defensivo), 3 cartas/turno, chassi −6 | **frame Vira-lata**, sem defensivo, **4 cartas/turno**, chassi +4 |

Os bônus de chassi foram feitos para a regra antiga (vida = torso + chassi) e foram refeitos do zero.
O chefe no frame Bastião gerava ~170 de blindagem por luta e a luta durava 18 turnos.

### Antes / depois (500 lutas por confronto)

| Categoria | Meta | Greedy antes → depois | Aleatório antes → depois | Mediana de turnos antes → depois |
|---|---|---|---|---|
| Fácil | > 80% | 100% → 99.9% | 97.7% → 78.5% | 2.6 → 4.1 |
| Moderado | 65–75% | 66.6% → 68.6% | 1.6% → 1.7% | 6.2 → 8.1 |
| Difícil | 55–60% | 59.3% → 56.5% | 1.4% → 1.2% | 7.0 → 7.7 |
| Chefe | 55–60% | 56.8% → 56.0% | 2.3% → 0.4% | 7.3 → 8.0 |

| Build (greedy) | Moderado | Difícil | Chefe |
|---|---|---|---|
| Inicial (Vira-lata Ferrovelho) | 73.5% → 80.7% ⚠ | 78.3% → 75.2% ⚠ | 54% → 62.8% |
| Vira-lata Kagemori | 64.6% → 67.1% | 61.9% → 47.8% | 63.6% → 41.4% ⚠ |
| Vira-lata Nyx | 62.7% → 65.9% | 45.4% → 49% | 52.4% → 55.6% |
| Bastião Bastion | 74.3% → 55% ⚠ | 58.8% → 44.4% ⚠ | 65.8% → 61.2% |
| Duelista Ferrovelho | 64.7% → 61.6% | 66.2% → 60.9% | 50.2% → 46.6% ⚠ |
| Duelista Kagemori + Ferrovelho | 59.6% → 81.3% ⚠ | 45.1% → 61.5% | 55% → 68.6% ⚠ |

Os 5 dossiês foram reescritos e estão de acordo com os resultados (o teste confere).

## 2026-11-10 — equilíbrio entre builds (`feature/build-balance`)

### Antes / depois (vitória do jogador greedy, média por categoria)

| Build | Moderado | Difícil | Chefe |
|---|---|---|---|
| Inicial (Vira-lata Ferrovelho) | 75% → 73.5% | 55.4% → 78.3% ⚠ | 41.2% → **54%** |
| Vira-lata Kagemori | 86.9% → 64.6% | 70.5% → 61.9% | 79.4% → 63.6% |
| Vira-lata Nyx | 90.7% → 62.7% | 82.7% → 45.4% ⚠ | 94% → 52.4% |
| Bastião Bastion | 20.4% → 74.3% | 6.1% → 58.8% | 3.6% → 65.8% |
| Duelista Ferrovelho | 75% → 64.7% | 61.5% → 66.2% | 53.8% → 50.2% |
| Duelista Kagemori + Ferrovelho | 82.5% → 59.6% ⚠ | 64.4% → 45.1% ⚠ | 67.6% → 55% |
| **Média (meta)** | 71.8% → 66.6% (65–75%) | 56.8% → 59.3% (55–60%) | 56.6% → 56.8% (55–60%) |

Diferença entre builds no Chefe caiu de 90 pontos (3.6%–94%) para 15 (50%–66%).
⚠ = mais de 10 pontos fora da meta. O que sobra vem de um confronto de "pedra-papel-tesoura":
a Rainha do Ácido vence Nyx e Duelista misto (~35%) e perde para a build inicial (86%).

### Regras (código)

| O quê | Antes | Depois | Por quê |
|---|---|---|---|
| Corrosão | causa N de dano por turno e cai 1 (acumulava de forma explosiva) | **1 de dano por turno**, N = turnos restantes | Era o que deixava o Nyx em 94% contra o chefe. Também bate com o documento: "um dano por turno" e o bônus Nyx "status duram 1 turno a mais". |
| Sobrecarga | acumulava turnos (3 Choques = 3 turnos de −1 energia) | **não acumula**: reaplicar só renova a duração | O documento diz "1 energia a menos no próximo turno"; acumular travava o inimigo (Kagemori 98% contra o Bastião). |

### Cartas

| Carta | Antes | Depois | Por quê |
|---|---|---|---|
| Investida | dano = metade da sua blindagem | **dano = toda a sua blindagem** | A build de blindagem (Bastion) não tinha como converter defesa em dano (6% no Difícil). |
| Desmontar | 8 de dano | **7 de dano, ignora blindagem** | A build inicial não tinha resposta a blindagem e perdia para o chefe (41%). |
| Corte Preciso | custo 2 | **custo 3** | Kagemori é "precisa e cara"; com o bônus Kagemori ela saía quase de graça. |
| Choque | 4 de dano | **3 de dano** | Junto da Sobrecarga, era a carta mais eficiente da Kagemori. |

### Inimigos (ajuste fino)

| Inimigo | Antes | Depois | Por quê |
|---|---|---|---|
| Bastião de Patrulha | defensivo + cirurgião, chassi +6 | **cirurgião + sucateiro**, chassi +4 | Defensivo desperdiçava jogadas e era atropelado por builds que ignoram blindagem. |
| Lâmina Corporativa | chassi +17 | chassi +22 | Recuperar a meta depois do corte do Choque/Corte Preciso (cartas dela também). |
| Kiro Sato | chassi +12 | chassi +13 | Idem. |
| Rainha do Ácido | Lança-chamas no braço direito, chassi +15 | **Lâmina de Plasma**, chassi +34, +1 energia | Com a Corrosão nova ela ficou fácil; ganhou um ataque que ignora blindagem. |
| Mandíbula (chefe) | Fase 2: +1 energia e Mordida; chassi −8 | Fase 2: só a Mordida; chassi −6 | A energia extra na fase 2 punia demais builds sem resposta à blindagem. |

### Jogador automático (simulador)

- Passa a dar valor à blindagem mesmo sem ataque visível, mais ainda com o frame Bastião (a blindagem dura) e com Investida na mão.
  Antes a build Bastion desperdiçava ~5–10 de energia por luta e o simulador media o bot, não a build.
- Valor de Corrosão ajustado para a regra nova (1 por turno).

### Sem mudança

Bônus Ferrovelho (2 de blindagem por sucata) e Bastion (6 de blindagem inicial) continuam iguais, agora configuráveis em `content.json` (`makers[].value`).
