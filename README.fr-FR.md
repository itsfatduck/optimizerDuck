<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="./.github/assets/optimizerDuck.png" alt="optimizerDuck Banner" title="optimizerDuck"/></a>

# [optimizerDuck](https://optimizerduck.vercel.app/)

**optimizerDuck est un outil d'optimisation Windows gratuit et open-source axé sur la performance, la confidentialité et la simplicité.**

[![Release](https://img.shields.io/github/release/itsfatduck/optimizerDuck?color=fed114&label=Version&style=flat-square)](https://github.com/itsfatduck/optimizerDuck/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/itsfatduck/optimizerDuck/total?label=T%C3%A9l%C3%A9chargements&style=flat-square&color=lightgreen)](https://github.com/itsfatduck/optimizerDuck/releases)
[![Stars](https://img.shields.io/endpoint?url=https://api.pinstudios.net/api/badges/stars/itsfatduck/optimizerDuck&style=flat-square)](https://github.com/itsfatduck/optimizerDuck/stargazers)
[![License](https://img.shields.io/badge/Licence-GPL_v3-red?style=flat-square)](./LICENSE)
[![Discord](https://img.shields.io/discord/1091675679994675240?color=5865f2&label=Discord&style=flat-square)](https://discord.gg/tDUBDCYw9Q)
<br>
[![CI](https://github.com/itsfatduck/optimizerDuck/actions/workflows/ci.yml/badge.svg)](https://github.com/itsfatduck/optimizerDuck/actions/workflows/ci.yml)
[![.NET Latest](https://img.shields.io/badge/.NET_Runtime-Derni%C3%A8re_version-ef99dd?style=flat-square)](https://dotnet.microsoft.com/en-us/download)
[![Supported OS](https://img.shields.io/badge/Support%C3%A9-Windows_10%2B_x64-0078d4?style=flat-square)](https://www.microsoft.com/en-us/software-download/)

**[Démarrage](https://optimizerduck.vercel.app/docs/guides/getting-started) | [Comment ça marche](https://optimizerduck.vercel.app/docs/guides/how-it-works) | [FAQ](https://optimizerduck.vercel.app/docs/faq/general)**

[English](README.md) | [Tiếng Việt](README.vi.md) | [繁體中文](README.zh-TW.md) | [简体中文](README.zh-CN.md) | [Русский](README.ru-RU.md) | **Français** | [Español](README.es-ES.md) | [한국어](README.ko-KR.md)

<details>
<summary>⭐ Historique des étoiles</summary>

Si optimizerDuck vous a aidé à améliorer votre PC, pensez à donner une ⭐ au repo et à le partager avec d'autres.
Chaque étoile aide à motiver les améliorations futures.

<a href="https://www.star-history.com/#itsfatduck/optimizerDuck&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&legend=top-left" />
 </picture>
</a>

</details>

<img src="./.github/assets/app.png" alt="optimizerDuck Mode sombre" title="optimizerDuck Mode sombre" width="800"/>

</div>

---

## Démarrage rapide

1. Téléchargez depuis **[GitHub Releases](https://github.com/itsfatduck/optimizerDuck/releases/latest)**
2. Exécutez le fichier `.exe` directement, aucune installation requise
3. Choisissez les optimisations souhaitez, appliquez-les et redémarrez votre PC lorsque vous êtes prêt

> [!TIP]
> Créez toujours un **point de restauration système** avant d'effectuer des modifications.

> [!NOTE]
> | | Langue | Nom d'origine | Traducteur |
> |------|----------|-------------|------------|
> | 🇺🇸 | English (United States) | English | Principal et recommandé |
> | 🇻🇳 | Vietnamese | Tiếng Việt | [itsfatduck](https://github.com/itsfatduck) |
> | 🇹🇼 | Traditional Chinese | 正體中文 | [abc0922001](https://github.com/abc0922001) |
> | 🇨🇳 | Simplified Chinese | 简体中文 | [wcxu21](https://github.com/wcxu21) |
> | 🇷🇺 | Russian | Русский | [Foodhead](https://github.com/Foodhead) |
> | 🇫🇷 | French | Français | [Robocnop](https://github.com/Robocnop) |
> | 🇰🇷 | Korean | 한국어 | [klfnn](https://github.com/klfnn) |
> | 🇪🇸 | Spanish | Español | [thexxtt](https://github.com/thexxtt) |

> Vous souhaitez ajouter votre langue ? Consultez [CONTRIBUTING.md](./CONTRIBUTING.md).

---

## Ce que fait optimizerDuck

Windows en lui-même est très stable. Mais une installation propre apporte aussi des services, de la télémétrie, des applications pré-installées et des tâches planifiées dont vous n'avez peut-être jamais entendu parler — tout tourne en arrière-plan, consommant votre CPU, RAM et disque. Pendant ce temps, certaines fonctionnalités qui pourraient vous aider à tirer le meilleur de votre matériel ne sont pas activées par défaut.

optimizerDuck vous offre une interface unique pour nettoyer le superflu et débloquer l'utile.

Il applique des tweaks système ciblés pour réduire la surcharge et bloquer les comportements indésirables, et intègre plusieurs outils de gestion pour vous permettre de voir ce qui fonctionne, de supprimer ce que vous ne voulez pas et d'annuler toute modification en cas de problème.

> [!NOTE]
> Toute optimisation peut être appliquée manuellement. optimizerDuck vous facilite simplement l'application de ces optimisations.

### Optimisations système

Plus de 30 tweaks répartis dans 6 catégories, chacun avec une description claire et un indice de risque pour que vous sachiez exactement ce que fait chaque modification avant de l'appliquer.

| Catégorie                  | Ce qu'elle couvre                                                                                                                                                                                                |
| :------------------------- | :--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Performance**            | Réglage du Service Host en fonction de votre RAM, ajustement de la priorité des processus, réduction de la latence du clavier et optimisation du planificateur multimédia pour une expérience de jeu plus fluide |
| **Confidentialité**        | Désactiver la télémétrie Windows, le rapport d'erreurs, l'ID publicitaire, la localisation, Cortana, Copilot et les suggestions de contenu                                                                       |
| **GPU**                    | Tweaks registre spéciques aux vendeurs pour GPU AMD, NVIDIA et Intel, couvrant les états d'alimentation, le clock gating et la latence d'affichage                                                               |
| **Alimentation**           | Désactiver l'hibernation et le démarrage rapide, désactiver la suspension sélective USB, installer un plan d'alimentation haute performance personnalisé et désactiver le throttling d'alimentation              |
| **Bloatware & Services**   | Bloquer le comportement de réinstallation des applications OEM et affiner les types de démarrage pour plus de 200 services Windows                                                                               |
| **Expérience utilisateur** | Supprimer les délais d'affichage des menus, désactiver les effets visuels comme les animations de la barre des tâches et la transparence pour une réactivité accrue                                              |

> [!NOTE]
> Les optimisations ici sont issues d'outils reconnus avec une large base d'utilisateurs — rien n'est généré par IA ou ajouté aveuglément. Chaque réglage est choisi pour son impact réel.

### Personnaliser

Pas besoin de fouiller le registre — interrupteurs, listes déroulantes et champs numériques présentés au même endroit. Quatre catégories :

- **Bureau** : Afficher ou masquer les icônes (Ce PC, Corbeille, Réseau, Fichiers utilisateur, Panneau de configuration), supprimer les flèches de raccourci
- **Préférences** : Alignement de la barre des tâches, widgets, boutons Task View et Fin de tâche, secondes sur l'horloge, mode sombre, extensions de fichiers, fichiers cachés, historique du presse-papiers, vue compacte, assistance au collage, cases à cocher, menu contextuel classique et recherche Bing
- **Jeux** : Mode Jeu, Barre de jeu, enregistrement en arrière-plan, accélération de la souris, optimisations plein écran, planification GPU accélérée par le matériel
- **Système** : Activer Num Lock au démarrage

### Outils intégrés

| Outil                 | Ce qu'il fait                                                                                                                                                                                                  |
| :-------------------- | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **System Dashboard**  | Affiche les informations de votre CPU, RAM, GPU, disques de stockage et les détails du système d'exploitation dans un seul panneau                                                                             |
| **Startup Manager**   | Liste toutes les applications et tâches qui se lancent au démarrage, les active ou désactive, et ouvre leur emplacement de fichier                                                                             |
| **Scheduled Tasks**   | Parcourt, exécute, arrête, active, désactive ou supprime les tâches planifiées Windows                                                                                                                         |
| **Disk Cleanup**      | Scanne et nettoie les fichiers temporaires, le cache système, les fichiers Windows Update inutilisés, le prefetch, les miniatures, la corbeille, les vidages de mémoire et les anciennes installations Windows |
| **Bloatware Remover** | Liste tous les packages AppX supprimables avec des badges de risque (Sûr, Attention, Inconnu), afin que vous puissiez choisir ce que vous souhaitez supprimer                                                  |

---

## Sécurité

Modifier les paramètres système comporte des risques. optimizerDuck est construit autour de la réversibilité et du contrôle utilisateur.

Consultez la [Politique de confidentialité](./PRIVACY.md) pour plus de détails sur nos pratiques de données.

- **Sauvegardes automatiques** : Chaque modification écrit un fichier d'annulation dans un dossier local. Vous pouvez restaurer des tweaks individuels ou tout rétablir
- **Annulation en un clic** : Annulez toute optimisation appliquée directement depuis l'interface
- **Indices de risque** : Chaque tweak est étiqueté Sûr, Modéré ou Risqué en fonction de son impact potentiel
- **Aucune valeur par défaut appliquée** : Rien ne s'exécute tant que vous ne le sélectionnez. L'outil n'active rien de lui-même
- **Invitation de point de restauration** : Avant votre première optimisation, l'application vous propose de créer un point de restauration Windows

---

## FAQ

### C'est sûr d'utiliser optimizerDuck ?

Oui. optimizerDuck est complètement **open-source** (GPL v3), donc n'importe qui peut inspecter le code, l'auditer ou le compiler lui-même. Chaque version est compilée automatiquement par **GitHub Actions** à partir du code source public — pas de modifs cachées, pas de binaire non signé injecté après la compilation. Si tu veux, tu peux cloner le repo et builder le `.exe` toi-même avec une simple commande `dotnet build`.

L'app **ne collecte rien** : pas de télémétrie, pas de données d'utilisation, pas d'infos personnelles. Voir la [Politique de confidentialité](./PRIVACY.md).

### optimizerDuck améliore vraiment les perfs, réduit la latence ou accélère le réseau ?

Ça peut aider. Chaque optimisation dans optimizerDuck est **tirée d'outils connus, de guides communautaires et de recommandations des fabricants** — rien n'est généré par IA, ajouté au hasard ou inventé. Chaque réglage touche un vrai paramètre que Windows configure trop prudemment par défaut (groupement des services hôtes, états d'alimentation GPU, limitation réseau, ordonnancement des processus).

Y'a pas de faux hacks de registre ici, chaque modif a un but documenté et un impact réel confirmé par la communauté et les specs constructeurs.

### Pourquoi Windows SmartScreen / Defender bloque le téléchargement ?

Parce qu'optimizerDuck n'est pas signé numériquement — les certificats de signature de code coûtent une blinde pour un projet open-source. Quand Windows rencontre un exe non signé téléchargé depuis Internet, SmartScreen affiche un avertissement par défaut. C'est normal, ça **veut pas dire** que le fichier est dangereux.

Pour passer : clique sur **"Plus d'informations" > "Exécuter quand même"**. Si tu flippes encore :
- Compile le `.exe` toi-même depuis les [sources](https://github.com/itsfatduck/optimizerDuck)
- Balance le binaire dans un sandbox en ligne comme ANY.RUN pour vérifier

### Je peux annuler les changements si ça foire ?

Oui. Chaque optimisation crée un fichier d'annulation avant de s'appliquer. Tu peux défaire des réglages individuels ou tout restaurer d'un clic depuis l'interface. L'app te proposera aussi de créer un point de restauration Windows avant ta première optimisation.

### Ça marche sur Windows 10 et Windows 11 ?

Oui. optimizerDuck supporte **Windows 10 (x64)** et **Windows 11 (x64)**.

### Il faut les droits administrateur ?

Oui. Comme il modifie les paramètres système et le registre Windows, il doit être lancé en mode administrateur.

### Est-ce qu'optimizerDuck collecte mes données ?

Non. L'app contient zéro télémétrie, zéro analytique, zéro fonction qui téléphone à la maison. Elle tourne entièrement hors ligne et n'envoie rien nulle part.

---

## Détails techniques

- **Framework** : WPF sur .NET 10, utilisant la bibliothèque WPF UI pour le design Fluent
- **Système d'annulation** : Quatre types d'étapes d'annulation (Registre, Service, Tâche planifiée, Shell) avec état persisté en JSON et E/S de fichiers thread-safe
- **Thématisation** : Modes Sombre (par défaut), Clair et Contraste élevé avec support du fond Mica
- **Pas d'installation** : S'exécute en tant que fichier .exe unique, aucune installation requise
- **Système de sauvegarde** : Dossier local de sauvegarde pour chaque modification, restauration en un clic
- **Découverte** : Les catégories d'optimisations et de fonctionnalités sont découvertes automatiquement via le reflet + attributs personnalisés, sans enregistrement manuel nécessaire
- **Pas de télémétrie** : L'application ne collecte aucune donnée utilisateur

---

## Documentation

### [Documentation officielle](https://optimizerduck.vercel.app/docs/guides/getting-started)

Guides, détails des optimisations et conseils d'utilisation.

---

## Contribuer

Les rapports de bugs, les nouvelles optimisations, les améliorations de documentation et les traductions sont tous les bienvenus. Consultez [CONTRIBUTING.md](./CONTRIBUTING.md).

---

## Communauté

> [!TIP]
> Rejoignez notre serveur Discord pour obtenir de l'aide, des astuces et discuter avec d'autres utilisateurs et contributeurs.
>
> <a href="https://discord.gg/tDUBDCYw9Q"><img src="https://discord.com/api/guilds/1091675679994675240/widget.png?style=banner2" alt="Discord Banner 2"/></a>

Si optimizerDuck a aidé votre PC :

- ⭐ Mettez une étoile au repo
- 💬 Rejoignez Discord pour obtenir de l'aide
- 🐞 Signalez les bugs sur GitHub
- 🎁 Soutenez le projet [ici](https://optimizerduck.vercel.app/docs/contribute/support-me)

### Liens

- 🌐 [Site web](https://optimizerduck.vercel.app/)
- 📖 [Documentation](https://optimizerduck.vercel.app/docs/guides/getting-started)
- 💬 [Discord](https://discord.gg/tDUBDCYw9Q)
- 🐞 [Issues](https://github.com/itsfatduck/optimizerDuck/issues)

Les rapports de bugs, les suggestions de fonctionnalités, les traductions et le partage de votre expérience aident tous le projet.

---

## Avertissement

optimizerDuck est fourni **"en l'état"**, sans garantie d'aucune sorte.

En utilisant cet outil, vous acceptez que les auteurs ne soient pas responsables de l'instabilité du système, de la perte de données ou des problèmes causés par des logiciels tiers ou des modifications utilisateur.

Créez toujours un **point de restauration** avant d'appliquer des modifications.

> [!NOTE]
> optimizerDuck modifie les paramètres système et le registre Windows. Utilisez à vos propres risques. Nous recommandons de sauvegarder vos données importantes et de créer un point de restauration avant d'effectuer des modifications.
>
> Consultez [Conditions d'utilisation](./TERMS.md), [Politique de confidentialité](./PRIVACY.md) et [Avertissement](./DISCLAIMER.md) pour plus d'informations.

---

## Licence

<div align="center">

<a href="./LICENSE">
<img src=".github/assets/gplv3.png" alt="Licence GPL v3" title="Licence GPL v3"/>
</a>

**[Licence GPL v3](https://www.gnu.org/licenses/gpl-3.0.en.html)**<br>Voir [LICENSE](./LICENSE).

</div>

<div align="center">

## Merci à tous les contributeurs

[![Contributors](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
