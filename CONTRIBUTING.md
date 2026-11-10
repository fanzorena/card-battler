# Como contribuir

O repositório usa **Git Flow**. Tudo fica local até alguém decidir publicar.

## Branches

- **main**: só a versão estável do último release. Ninguém commita direto nela.
- **develop**: onde o desenvolvimento acontece. Toda feature volta para cá.
- **feature/\<nome\>**: uma por funcionalidade. Sai de `develop` e volta com `git merge --no-ff`.
- **release/\<versão\>**: sai de `develop` quando um conjunto de features está pronto. Só recebe correções. Ao fechar, entra em `main` e em `develop` (ambos com `--no-ff`) e ganha a tag `v<versão>` em `main`.
- **hotfix/\<versão\>**: sai de `main` para corrigir algo urgente no release. Ao fechar, entra em `main` e em `develop` e ganha a tag `v<versão>`.

Tags de versão usam o prefixo `v` (ex.: `v0.1.0`) e seguem versionamento semântico.

## Exemplo

```bash
git checkout develop
git checkout -b feature/nova-carta
# ... commits ...
git checkout develop
git merge --no-ff feature/nova-carta
git branch -d feature/nova-carta
```

Release:

```bash
git checkout -b release/0.2.0 develop
# ... ajustes finais ...
git checkout main && git merge --no-ff release/0.2.0 && git tag -a v0.2.0 -m "v0.2.0"
git checkout develop && git merge --no-ff release/0.2.0
git branch -d release/0.2.0
```

## Antes de fazer merge

- Rode os testes: `unity test . --mode EditMode` (com o Editor fechado) ou `unity command test run -- --mode editor` (com o Editor aberto).
- Mensagens de commit em português, curtas e no imperativo.
