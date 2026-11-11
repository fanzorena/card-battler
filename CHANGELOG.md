# Changelog

Versões seguem o Git Flow do projeto (`main` = último release estável). Detalhes de balanceamento em `BALANCE_CHANGES.md`.

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
