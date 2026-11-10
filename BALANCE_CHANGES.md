# Mudanças de balanceamento

Valores que mudaram em relação ao documento de design / versão anterior, para atualizar o documento.
Medido com o simulador (Card Battler > Simulador), 500 lutas por confronto, jogador greedy.

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
