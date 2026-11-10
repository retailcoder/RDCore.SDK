# RDCore.SDK
<sup>_This document is available in [English](./README.md)_</sup>

![VIVAT CUCUMIS](./assets/vivat-cucumis-stonecore.png)

## Avant de commencer.
> 👋 Bonjour! Nouveau ici? _Rubberduck_ a toujours été une initiative open-source. **RDCore l'honore avec une formule Open-Core**.  
> <small>Voir [rubberduckvba.ca](https://rubberduckvba.ca) pour plus d'informations.</small>

Ce dépôt contient le paquet [**RDCore.SDK**](https://www.nuget.org/packages/RDCore.SDK) de la plateforme [RDCore](https://github.com/rubberduck-vba/RDCore), sous licence **⚖️MIT**. Le paquet contient ces bibliothèques, chacune un assembly distinct :

- **RDCore.SDK** : le modèle du langage (types, symboles, arbres syntaxiques, sémantique et diagnostics MS-VBAL) et la plomberie que partagent tous les composants de la plateforme (hébergement, configuration, journalisation, espaces de travail, bases des applications serveur et client). Chaque composant et chaque extension de RDCore s'appuie sur elle.
- **RDCore.SDK.Protocol** : ce dont un client de la plateforme a besoin, et rien d'autre. Elle contient les méthodes `rdcore/*` et leurs messages, la poignée de main de la plateforme, et comment démarrer le serveur de langage. Elle n'utilise que System.Text.Json, sans OmniSharp. L'add-in RDCore de HexIDE la référence.

La plateforme construite sur ces bibliothèques, incluant le runtime RD-VBA, se trouve dans le dépôt [RDCore](https://github.com/rubberduck-vba/RDCore), sous **⚖️GPLv3 ou toute version ultérieure**. Ce que vous construisez avec RDCore vous appartient (voir l'[AVIS de RDCore](https://github.com/rubberduck-vba/RDCore/blob/main/NOTICE.md)).

La licence MIT de [LICENSE](LICENSE) couvre les bibliothèques de ce dépôt. Une version française de la licence est disponible sur demande.

## 📊 État du projet
Ces bibliothèques sont en version **alpha** : leur API changera encore avant la version 1.0.

**Contributions**  

- Individus : ✅ Ouvertes ([CLA](CLA.fr.md))  
- Corporatives : ⏳ Prévues  

[![NuGet](https://img.shields.io/nuget/vpre/RDCore.SDK)](https://www.nuget.org/packages/RDCore.SDK)
[![Build and Test](https://github.com/rubberduck-vba/RDCore.SDK/actions/workflows/build.yml/badge.svg)](https://github.com/rubberduck-vba/RDCore.SDK/actions/workflows/build.yml)
[![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/rubberduck-vba/RDCore.SDK/badges/coverage.json)](https://github.com/rubberduck-vba/RDCore.SDK/actions/workflows/build.yml)

| Bibliothèque | Rôle | Couverture |
|---|---|---|
| **RDCore.SDK** | Cœur du langage + plomberie partagée | [![RDCore.SDK](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/rubberduck-vba/RDCore.SDK/badges/coverage-RDCore.SDK.json)](https://github.com/rubberduck-vba/RDCore.SDK/actions/workflows/build.yml) |
| **RDCore.SDK.Protocol** | Le protocole côté client, sans OmniSharp | [![RDCore.SDK.Protocol](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/rubberduck-vba/RDCore.SDK/badges/coverage-RDCore.SDK.Protocol.json)](https://github.com/rubberduck-vba/RDCore.SDK/actions/workflows/build.yml) |

## Compiler

À partir de la racine du dépôt, avec le SDK .NET 10 :

```
dotnet build RDCore.SDK.slnx
dotnet test RDCore.SDK.slnx
```

---
### Voir aussi
- [La référence de l'API du SDK](https://rubberduck-vba.github.io/RDCore.SDK/api/RDCore.SDK.html)
- [RDCore](https://github.com/rubberduck-vba/RDCore)
- [CONTRIBUTING.md](CONTRIBUTING.md)
- [CODE_OF_CONDUCT.fr.md](CODE_OF_CONDUCT.fr.md)
- [RD-VBAL](https://rubberduck-vba.github.io/RDCore/specs/rd-vbal.1.0.introduction.html)

> [!NOTE]
> La version française des documents techniques, lorsque disponible, utilise les termes originaux _en anglais_ qui conservent la précision de leur signification, plutôt qu'une traduction approximative qui pourrait facilement être plus confondante qu'utile.

<hr/>
<p align='left' style='margin-left: 32px;'>
<a href='https://rubberduck-vba.github.io/RDCore/index.fr.html'>ACCUEIL</a> • <a href='https://rubberduck-vba.github.io/RDCore/index.html'>HOME</a>  | ℹ️ <a href='https://rubberduck-vba.github.io/RDCore/introduction.fr.html'>BIENVENUE</a> • <a href='https://rubberduck-vba.github.io/RDCore/introduction.html'>WELCOME</a>  | 🧩 <a href='https://rubberduck-vba.github.io/RDCore/getting-started.fr.html'>BÂTISSONS</a> • <a href='https://rubberduck-vba.github.io/RDCore/getting-started.html'>BUILD</a>  | <a href='https://rubberduck-vba.github.io/RDCore/specs/rd-vbal.html'><strong>RD-VBAL</strong></a>  |  <a href='https://rubberduck-vba.github.io/RDCore.SDK/api/RDCore.SDK.html'>SDK</a>  | 🌐 <a href='https://rubberduckvba.ca'>rubberduckvba.ca</a>
</p>
<hr/>
<p align='center'><img alt='Logo™ 9562-7303 Québec inc.' src='./assets/vector-ducky.svg' style='width:200px; align:center;' /></p>
<h6 align='center'>V I V A T ❤️ C U C U M I S ™</h6>
<p align='center' style='font-size:8pt;'>
<small>© Copyright <strong>9562-7303 Québec inc.</strong> (2026)<br/><em>Seul, &quot;Rubberduck&quot; est utilisé pour fins de référence au projet open-source legacy <strong>utilisé publiquement ainsi depuis 2015</strong> et sans lien ni affiliation avec tout tiers détenteur d'une marque semblable dans quelque juridiction que ce soit.<br/>&quot;Rubberduck VBA&quot;, &quot;RDCore&quot; et &quot;VIVAT CUCUMIS&quot; sont des marques de commerce revendiquées par 9562-7303 Québec inc. (en attente); Toutes les marques appartiennent à leur détenteur respectif.<br/>RDCore n'est pas un produit de Microsoft et n'est pas affilié à Microsoft, ni directement, ni indirectement.<br/><br/>If used alone, <em>&quot;Rubberduck&quot; is used as a reference to the legacy open-source project <strong>the same way it has been used publicly since 2015</strong> and without any links or affiliation with any third-party trademark holders of a similar trademark in any jurdisdiction.<br/>&quot;Rubberduck VBA&quot;, &quot;RDCore&quot; and &quot;VIVAT CUCUMIS&quot; are trademarks claimed by 9562-7303 Québec inc. (pending). All trademarks belong to their respective owners.<br/>RDCore is not a Microsoft product and is not affiliated with Microsoft, directly or indirectly.</small>
</p>

