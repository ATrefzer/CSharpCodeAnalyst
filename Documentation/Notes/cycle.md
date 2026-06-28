# Cycle Ergänzungen

Ich schaue mir zuerst die Doku und den Algorithmus an, bevor ich antworte.

Sehr aufschlussreich. Jetzt schaue ich mir den Regressionstest an, der in den Kommentaren referenziert wird, sowie die Hilfsmethoden — das hilft, das `GetContainerLevel`-Problem zu rekonstruieren.

Ich habe jetzt alles beisammen: `CycleFinder`, `SearchGraphBuilder`, `CodeGraphBuilder`, `CodeElementClassifier` und den entscheidenden Test `Regression_NestedNamespaces`. Damit lassen sich beide Fragen sauber rekonstruieren.

## Frage 1: Warum ist `set_TableData` im Graph, obwohl er nicht im Zyklus ist?

Der Kern liegt darin, **auf welcher Granularität** der Zyklus gefunden wird vs. **auf welcher Granularität** er wieder aufgefaltet wird.

**Wo der Zyklus gefunden wird:** Die Suche läuft nicht auf den Accessoren, sondern auf dem **Property `TableData` als Proxy-Knoten**. Schau dir `GetHighestElementsInvolvedInDependency` an: Für die Abhängigkeit `get_TableData → TableDataProperty` (Getter liest das Backing-Field) ist der niedrigste gemeinsame Vorfahre die Klasse. Das höchste beteiligte Element *unterhalb* der Klasse ist auf der Getter-Seite nicht `get_TableData`, sondern dessen Eltern-Property **`TableData`**. Also wird die Kante zu `TableData(property) → TableDataProperty(field)` angehoben. Genau dieses Property landet in der SCC — nicht der Getter, nicht der Setter.

**Was beim Auffalten passiert:** In `CodeGraphBuilder.GenerateDetailedCodeGraph` wird jeder Proxy-Knoten über **alle seine Kinder inklusive sich selbst** expandiert ([CodeGraphBuilder.cs:26](https://claude.ai/epitaxy/CodeGraph/Algorithms/Cycles/CodeGraphBuilder.cs#L26)):

```
allSources = TableData.GetChildrenIncludingSelf()  // = { TableData, get_TableData, set_TableData }
```

Dann sucht `GetOriginalDependencies` **alle** Original-Kanten von dieser Quellmenge zu den Zielen (dem Backing-Field). Und hier liegt der Hund begraben:

- `get_TableData → TableDataProperty` (Getter liest Feld) → ist Teil des Zyklus
- `set_TableData → TableDataProperty` (Setter schreibt Feld) → ist **nicht** Teil des Zyklus, wird aber trotzdem gefunden

Beide Kanten gehen von einem Kind des Property zum selben Field. Die **eine** Proxy-Kante `TableData → TableDataProperty` steht stellvertretend für **beide** konkrete Kanten. Beim Auffalten kann der Algorithmus nicht mehr unterscheiden, welche der beiden Original-Kanten den Kreis schließt — er materialisiert einfach alle, die der Proxy repräsentiert hat. Damit zieht die Setter-Kante `set_TableData` als Quell-Endpunkt mit in den Graph.

**In Worte gefasst:** Der Setter ist *„guilty by containment"*. Er ist Mitglied eines Containers (das Property), der im Zyklus steckt, und er teilt sich mit dem Getter dieselbe Abhängigkeit aufs Backing-Field, die der Zyklus benutzt. Der Algorithmus zeigt bewusst **alle realen Kanten, die die kollabierte Proxy-Kante repräsentiert hat**, statt die Zyklensuche nochmal auf Accessor-Ebene laufen zu lassen, um nicht-beteiligte Kanten herauszufiltern. Das Property ist für die Suche ein unteilbares Atom; beim Zurückfalten wird das Atom nicht erneut gespalten und neu bewertet.

Das ist sogar halbwegs erwünscht — du siehst die *komplette* Kopplung des Property ans Field. Strikt genommen ist die Setter-Kante aber Beifang.

## Frage 2: Die Rolle von `GetContainerLevel` — rekonstruiert über `Regression_NestedNamespaces`

`GetContainerLevel` vergibt einen **Hierarchie-Rang**, unabhängig von der tatsächlichen Baumtiefe: Namespace/Assembly = 30, Typ = 20, Member (Methode/Field/Property…) = 0. Sie wird an **zwei** Stellen gebraucht, und beide lösen dasselbe Grundproblem: *gleicher Rang = vergleichbare Container*.

### Verwendung A — Proxy-Endpunkte auf gleichen Rang heben (`SearchGraphBuilder`, [Z. 128–152](https://claude.ai/epitaxy/CodeGraph/Algorithms/Cycles/SearchGraphBuilder.cs#L128))

Nach dem Entfernen der gemeinsamen Vorfahren können die beiden „höchsten beteiligten" Elemente auf **unterschiedlichem Rang** liegen — z. B. eine Klasse auf der einen, ein Namespace auf der anderen Seite. Die `while`-Schleife klettert die niedrigere Seite hoch, bis beide auf gleichem Rang sind. Ergebnis: Proxy-Kanten verlaufen immer Namespace↔Namespace oder Typ↔Typ, nie schräg. Das hält die SCC-Bildung konzeptionell sauber: Zyklen werden zwischen *Geschwistern gleichen Rangs* gemeldet.

### Verwendung B — das eigentliche Problem, das du suchst (`CodeGraphBuilder`, [Z. 39–46](https://claude.ai/epitaxy/CodeGraph/Algorithms/Cycles/CodeGraphBuilder.cs#L39))

Lass uns `Regression_NestedNamespaces` durchspielen. Die Struktur:

```
NS_Parent
├── ClassInParent
│   └── _delegate1 ─────────────► DelegateInChild   (Uses)
├── NS_Child                     (NESTED im Parent!)
│   ├── ClassInChild
│   │   └── Method ─────────────► ClassInParent      (Uses)
│   └── DelegateInChild
└── NS_Irrelevant
    └── ClassNsIrrelevant
        └── _delegate2 ─────────► DelegateInChild   (Uses)
```

Der Zyklus ist **NS_Parent ↔ NS_Child** (Parent zeigt via `_delegate1` hinein, Child zeigt via `Method` zurück). `NS_Child` liegt aber **innerhalb** von `NS_Parent`. Bei der Rückfaltung der Proxy-Kante `NS_Parent → NS_Child` gilt also `proxySource.IsParentOf(proxyTarget)`, und wir müssen entscheiden: *Welche Kinder von `NS_Parent` zählen als gültige Quellen?*

Naiv wäre `sources = NS_Parent.GetChildrenIncludingSelf()` — also **der ganze Teilbaum**, inklusive `NS_Irrelevant` und `_delegate2`. Dann würde `_delegate2 → DelegateInChild` (Ziel liegt in `NS_Child`) als gültige Kante gefunden und `NS_Irrelevant`, `ClassNsIrrelevant`, `_delegate2` in die Cycle-Group gezogen — obwohl `NS_Irrelevant` mit dem Zyklus **nichts** zu tun hat (es zeigt nur in `NS_Child` hinein, nichts kommt zurück). Der Test würde statt 7 dann 10 Knoten sehen.

Genau das verhindert `GetContainerLevel`:

```csharp
var parentLevel = GetContainerLevel(proxySource.ElementType);          // Namespace = 30
var children = proxySource.Children.Where(c =>
    GetContainerLevel(c.ElementType) < parentLevel);                   // nur Typen (20), NICHT NS_Child/NS_Irrelevant (30)
```

Damit ist die Aussage: **„Der Teil von `NS_Parent`, der wirklich `NS_Parent` \*ist\*, sind seine eigenen direkt enthaltenen Typen und deren Member — nicht die Namespaces, die zufällig in ihm verschachtelt liegen."** Geschwister-Namespaces gleichen Rangs werden abgeschnitten. Deshalb landen nur `ClassInParent`/`_delegate1` in den Quellen, `_delegate2` bleibt draußen, und das Ergebnis sind exakt die 7 erwarteten Knoten.

Das Rang-Schema (30 > 20 > 0) kodiert also „echtes untergeordnetes Inhalt vs. gleichrangiger Container, der nur räumlich drinsteckt". Ohne diese Unterscheidung würde die Rückfaltung über fremde Sub-Namespaces hinweg in unbeteiligte Abhängigkeiten greifen und die Cycle-Group verschmutzen.

Das deckt sich mit dem Doku-Abschnitt *„Handling containment when transforming back"* (`T = C / S`, „include all types in this namespace but not those in other namespaces") — nur dass die Doku nicht klar sagt, *warum* `GetContainerLevel` das Werkzeug dafür ist und welcher konkrete Bug (`NS_Irrelevant` wird mitgezogen) ohne sie auftritt.

------

Soll ich das Wissen festhalten? Ich würde Folgendes vorschlagen — sag mir, was du möchtest:

1. **`Documentation/cycle-detection.md`** um einen kurzen Abschnitt „Role of `GetContainerLevel`" ergänzen (beide Verwendungen + das `NS_Irrelevant`-Beispiel als das konkrete Problem, das es löst).
2. **Code-Kommentare** an den beiden `GetContainerLevel`-Stellen präzisieren (aktuell verweist nur ein knapper Kommentar auf den Regressionstest).
3. **Die Setter-Beobachtung** als kurzen Absatz „Why non-cycle accessor edges appear" in der Doku ergänzen.

Ich kann auch nur erklären und nichts ändern, falls du den Algorithmus bewusst nicht anfassen willst (Doku/Kommentare ändern ja keine Logik).



Kurze Antwort: **Nein, nicht sauber** — und genau das ist das Interessante daran. Der naheliegende „einfache" Fix hat einen echten Korrektheitspreis. Lass mich das begründen, weil es auch erklärt, warum man es lieber so lässt.

## Der verlockende Fix — und warum er bricht

Die offensichtliche Idee: Nimm den fertigen `detailedGraph` und lass nochmal eine SCC-Erkennung **auf Element-Granularität** drüberlaufen; wirf alles raus, was in keiner nicht-trivialen SCC liegt. `set_TableData` hat keine eingehende Kante → fliegt raus. ~30 Zeilen, fasst den gefürchteten Kern (Tarjan/Proxy/Search-Graph) nicht an.

Das funktioniert für den TableData-Fall — **weil der hier zufällig ein echter Member-Level-Zyklus ist** (alle Beteiligten sind Methoden/Property/Field in derselben Klasse, eine echte elementare Schleife).

Aber es bricht den eigentlichen Daseinszweck des Algorithmus: **Container-Level-Zyklen.** Beispiel — Klasse A und B bilden einen Zyklus:

```
A.M1 ──► B.M1
B.M2 ──► A.M1
A.M3 ──► B.M1      ← echte A→B-Kopplung, aber NICHT auf einer elementaren Schleife
```

Der Zyklus existiert konzeptionell **zwischen den Klassen**, und auf Klassenebene trägt *jede* grenzüberschreitende Kante dazu bei — auch `A.M3 → B.M1`. Auf Methoden-Granularität liegt `A.M3` aber in keiner SCC (nichts ruft `M3` aus dem Kreis zurück auf). Ein blindes SCC-Pruning würde `A.M3` löschen und damit einen **legitimen Mitverursacher** der Klassen-Kopplung verstecken.

Genau das ist der Punkt: Die Setter-Kante und die `A.M3`-Kante sind für den Algorithmus **nicht unterscheidbar**. Beide sind „eine reale Kante hinter einer kollabierten Proxy-Kante, die selbst nicht auf einer elementaren Schleife liegt". Die eine willst du weg, die andere behalten — aber das Kriterium dafür kennt der Detailgraph nicht. Das „Setter-Rauschen" ist die Kehrseite genau des Features, das Container-Zyklen vollständig zeigt.

## Wo es theoretisch *doch* gezielt ginge

Der wahre Auslöser ist enger: Der Setter taucht nur auf, weil die Proxy-Anhebung den Getter **eine Ebene zu hoch** auf das Property hebt (`get_TableData` → `TableData(property)`), obwohl Getter und Field in *derselben* Klasse liegen. Das Property als Zwischen-Container ist ein Artefakt der „Split-Properties"-Änderung (Accessoren als Kinder des Property). Ohne diese Zwischenebene wäre der Proxy `get_TableData → field` und der Setter nie dabei.

Ein zielgerichteter Fix säße also in `GetHighestElementsInvolvedInDependency` — Properties für die Anhebung transparent behandeln, sodass der Accessor die Zyklus-Einheit bleibt. Das ist machbar, aber:

- es fasst **genau den Kern** an, den du nicht anrühren willst,
- Properties können auf höheren Ebenen (Klassenzyklus über Property-Typ) sehr wohl legitime Beteiligte sein — die Sonderbehandlung müsste das alles korrekt unterscheiden,
- und es bräuchte neue Approval-/Regressionstests.

## Empfehlung

Ich würde es **nicht lösen, sondern dokumentieren.** Es ist kein Bug, sondern die korrekte Konsequenz daraus, dass ein kollabierter Proxy beim Auffalten *alle* realen Kanten zeigt, die er repräsentiert hat. Der Setter ist informativ (volle Kopplung des Property ans Field) und harmlos — er verfälscht keinen Zyklus, er steht nur daneben.

Wenn dich der Detailgraph-Lärm später doch stört, ist der **gezielte** Property-Transparenz-Fix der richtige Hebel, nicht das nachträgliche SCC-Pruning — Letzteres würde echte Container-Zyklen beschädigen.

Soll ich den Trade-off (inkl. des `A.M3`-Gegenbeispiels) als kurzen Abschnitt „Why non-cycle edges appear in the result" in `cycle-detection.md` festhalten? Dann ist beim nächsten Mal sofort klar, warum das so bleibt.