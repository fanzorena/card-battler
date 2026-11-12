# Changelog

Versões seguem o Git Flow do projeto (`main` = último release estável). Detalhes de balanceamento em `BALANCE_CHANGES.md`.

## Unreleased (0.4.0)

- **Elementos:** todo ataque leva o elemento da peça de onde vem a carta. Cinético dá Impacto (+2 contra parte avariada ou chassi exposto), Térmico +1 Calor, Elétrico Sobrecarga e Químico +1 Corrosão na parte atingida (sem acumular com o mesmo status da carta). Algumas peças resistem a um elemento (−1 por golpe) ou são fracas a outro (+1). Chip do elemento nas cartas de ataque, efeito no tooltip, resistência/fraqueza na garagem; IA do jogador e dos inimigos valorizam o efeito. Rebalanceado (chassi extra de Kiro, Rainha do Ácido e Mandíbula) e dossiês do Kiro e do Mandíbula atualizados.
- **Terrenos:** cada arena tem uma regra. Piso de sucata (Fossos): pernas de rodas derrapam e esquivam só metade. Telhado na chuva (Distrito Neon): ataques Elétricos +2. Campo de supressão (Torre-Arena Kagemori): Calor recebido de ataques dobrado. Pernas Hover ignoram o terreno. A arena aparece no dossiê e na luta (com explicação no hover); trabalho e ranking são no Fosso; cada inimigo tem uma arena de casa para o Treino livre. Simulador usa a arena de casa e ganhou uma tabela por terreno. Rebalanceado (chassi extra de Bastião, Lâmina, Kiro, Rainha do Ácido e Mandíbula) e dossiês atualizados.
- **Animações de luta:** robôs balançam parados; quem ataca dá um passo à frente e dispara um tiro com rastro na cor do elemento; faíscas da cor do elemento no impacto; avisos flutuantes de Calor, Sobrecarga, Corrosão, energia perdida e rodas derrapando; na vitória/derrota o robô vencido desaba antes da tela de resultado. Opção "Reduzir animações" na tela inicial (desliga tremida, partículas, rastros, passos, balanço e queda; os números continuam).
- **Arte placeholder:** retratos em silhueta para Celeste, Kiro, Mandíbula, apostador velho, Mecânica e Locutor (diálogos, trabalho, Oficina, Fosso e dossiês do Kiro e do Mandíbula); cartões ilustrados dos lugares do mapa; céu noturno com lua, prédios, janelas e neon, reflexos na água; peças dos robôs com contorno, brilho, sombra e rebites. Tudo procedural e original.
- **Ferro-velho do Duda:** abre ao concluir o trabalho da Celeste ou ao virar campeão do Fosso. Loja de peças, frames (Bastião, Duelista) e planta do Reator K; venda de peças que nenhum dos 3 robôs usa (metade do preço). Trabalho curto do Duda (2 lutas) que dá a Planta do Hover. FABRICAR na Oficina funciona: planta + sucata + créditos viram peça.

## v0.3.0 — 2026-11-12

Primeira fatia de progressão: carreira com save, o trabalho da Celeste e o mapa do porto.

- **Visual e UX:** robôs placeholder procedurais, efeitos simples nas cartas, ícones de parte no lugar dos nomes (cartas, painéis, robô, intenções) e zona de Sucata para arrastar cartas.
- **Garagem:** separada da luta (Continuar leva à escolha do adversário), ficha do robô com "?" explicando cada número e comparação de peças.
- **Tela inicial:** Jogar/Continuar, Novo jogo, Treino livre (todas as peças liberadas), Sobre para playtesters, Histórico de lutas e Sair.
- **Trabalho da Celeste:** 4 lutas em sequência (Fácil → Moderado → Difícil → Mandíbula), dossiê antes de cada luta, dano que continua entre lutas, reparo com sucata ou créditos, recompensa de 1 entre 2 peças do robô vencido; derrota encerra o trabalho e cobra o reparo, nunca tira peças.
- **Carreira:** save com créditos, sucata, peças e frames possuídos, 3 robôs, dano e progresso; a garagem da carreira só usa peças que você tem.
- **Mapa do porto:** Bar Fim da Linha (diálogos com a Celeste, rumores do dossiê), Oficina (reparos e garagem), Fosso (ranking de 4 degraus até o Mandíbula; perder só custa a posição; o campeão pode defender o título), Ferro-velho trancado e diário de Pistas com a primeira pista sobre a luta do Tomás.
- **Lore:** falas alinhadas ao documento de design (Rafa, filho do Tomás; o Vira-lata era o chassi dele; núcleo Áurea); resumo em `Docs/lore.md`.
- **Simulador:** seção Trabalhos no relatório (taxa de conclusão do trabalho).
- **Projeto:** empresa Anzorena Games, splash da Unity desligado, ajustes de URP.

## v0.2.0 — 2026-11-11

Protótipo de combate jogável de ponta a ponta, com garagem, adversários com estratégia e ferramentas de balanceamento.

- **Combate:** custo 1 acerta parte aleatória e custo 2–3 escolhe a parte; o chassi só pode ser alvo depois que alguma parte for destruída; derrota = chassi em 0; comprar cartas custa energia. Calor, Sobrecarga, Corrosão, Encadear e Impacto revisados.
- **Garagem:** 22 partes e 3 frames do documento, regra de carga, bônus de fabricante, 3 robôs salvos, escolha de adversário.
- **Adversários:** 7 inimigos em 4 categorias (Fácil, Moderado, Difícil, Chefe) com estratégias combináveis (sucateiro, combista, cirurgião, defensivo, status, adaptativo) e um chefe com fases e golpe assinatura (Mandíbula).
- **Dossiê do adversário:** rumores e dicas antes da luta, conferidos automaticamente com o simulador.
- **Log de batalha:** cada luta gravada em JSONL + resumo legível.
- **Simulador de balanceamento:** milhares de lutas com jogador automático, relatório por categoria, build e inimigo (menu Card Battler > Simulador).

## v0.1.0 — 2026-11-10

Primeiro protótipo de combate 1 contra 1: build inicial do Vira-lata contra o Bastião de Patrulha, tela placeholder e testes das regras.
