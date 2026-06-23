Ich implementiere das. Zuerst schaue ich mir die relevanten Dateien an: `ParserConfig`, den Parser-Aufruf, die Settings/Preferences und den Settings-Dialog.

Jetzt suche ich, wo `ParserConfig` instanziiert wird und wie die Settings/Preferences und der Dialog aussehen.

Jetzt suche ich den Aufrufer von `ImportSolutionAsync` und wo `IncludeExternalCode` herkommt (Settings/Preferences + Dialog).

`IncludeExternalCode` ist die perfekte Vorlage. Ich lese die relevanten Dateien.

Jetzt MainViewModel (Aufrufstelle), ConsoleValidationCommand, appsettings.json und die Strings.resx-Einträge.

Jetzt schaue ich mir noch `SymbolExtensions.Key()` an (für die Stabilität der Symbol-Keys zwischen Phase 1 und 2) und die beiden Test-/Tool-Aufrufstellen.

Ich habe jetzt das vollständige Bild. `Key()` ist rein string-basiert und stabil über Compilations hinweg — ein generiertes Property, das in Phase 1 (über die generierte Datei) registriert wird, matcht in Phase 2 die Auflösung aus der User-Datei. Ich implementiere jetzt die Änderungen.

Beginnen wir mit dem Kern: `ParserConfig` und `HierarchyAnalyzer`.

Jetzt der HierarchyAnalyzer — die Projekt-Schleife und die `BuildHierarchy`-Methode:

Kern erledigt. Jetzt die Settings-Kette: `AppSettings`, `appsettings.json`, Dialog, Strings, und die Aufrufstellen.

Jetzt der Dialog (XAML + Code-Behind) und die Strings:

Jetzt die Resource-Strings. Zuerst die `.resx`, dann den generierten Designer:

Jetzt die Aufrufstellen: `Importer`, `MainViewModel`, `ConsoleValidationCommand` sowie die beiden Test-/Tool-Aufrufe.

Die beiden Test-/Tool-Aufrufstellen nutzen den optionalen dritten Parameter (Default `false`) und kompilieren unverändert weiter. Jetzt baue ich, um alles zu verifizieren.

Build ist grün (0 Fehler, 0 Warnungen). Zur Sicherheit ein Smoke-Test, dass der Default-Pfad (Flag aus) unverändert parst:

Alle Änderungen sind drin, Build grün, Default-Pfad unverändert (Test bestanden).

## Zusammenfassung der Änderungen

Die neue Option `IncludeGeneratedCode` schaltet ein, dass **Source-Generator-Dokumente** in Phase 1 erfasst werden. Damit bekommt z.B. ein `[ObservableProperty]`-generiertes Property ein eigenes `CodeElement` und der `Calls`-Edge landet beim Property statt per Fallback auf der Klasse.

**Parser-Kern:**

- [ParserConfig.cs](https://claude.ai/epitaxy/CodeParser/Parser/Config/ParserConfig.cs) — neuer optionaler Ctor-Parameter + Property `IncludeGeneratedCode` (Default `false`, damit bestehende Aufrufer unverändert kompilieren).
- [HierarchyAnalyzer.cs](https://claude.ai/epitaxy/CodeParser/Parser/HierarchyAnalyzer.cs) — bei aktivierter Option werden via `project.GetSourceGeneratedDocumentsAsync()` die generierten Dokumente geholt und durch denselben `ProcessNodeForHierarchy`-Walk geschickt. Das umgeht den `IsProjectFile`-Filter gezielt (generierte Trees sind weder in `project.Documents` noch in der Compilation enthalten).

**WPF/Settings:**

- [AppSettings.cs](https://claude.ai/epitaxy/CSharpCodeAnalyst/Configuration/AppSettings.cs) — Property + `Clone()`.
- [appsettings.json](https://claude.ai/epitaxy/CSharpCodeAnalyst/appsettings.json) — Default `false`.
- [SettingsDialog.xaml](https://claude.ai/epitaxy/CSharpCodeAnalyst/Configuration/SettingsDialog.xaml) / [SettingsDialog.xaml.cs](https://claude.ai/epitaxy/CSharpCodeAnalyst/Configuration/SettingsDialog.xaml.cs) — neue Checkbox „Include generated code" im Tab *Application Settings*.
- [Strings.resx](https://claude.ai/epitaxy/CSharpCodeAnalyst/Resources/Strings.resx) + [Strings.Designer.cs](https://claude.ai/epitaxy/CSharpCodeAnalyst/Resources/Strings.Designer.cs) — Label + Tooltip.

**Verkabelung:**

- [MainViewModel.cs:803](https://claude.ai/epitaxy/CSharpCodeAnalyst/MainViewModel.cs:803), [Importer.cs](https://claude.ai/epitaxy/CSharpCodeAnalyst/Features/Import/Importer.cs), [ConsoleValidationCommand.cs:93](https://claude.ai/epitaxy/CSharpCodeAnalyst/CommandLine/ConsoleValidationCommand.cs:93) — Option durchgereicht (analog zu `IncludeExternalCode`).

### Warum das den Fall löst

Der `Key()` eines Symbols ist rein string-basiert (Name/Kind/Parameter) und damit stabil über Compilations hinweg. Das in Phase 1 aus der generierten Datei registrierte Property bekommt denselben Key wie das in Phase 2 aus der User-Datei aufgelöste Property → `FindInternalCodeElement` trifft, der Step-2-Container-Fallback greift nicht mehr.

### Hinweise / Grenzen

- **Default ist `false`** — Verhalten bleibt für bestehende Projekte/Tests identisch (Cycles-Approval-Test bestanden).
- Es werden generierte **Member auf partiellen Typen** abgedeckt (der häufige MVVM-Toolkit-Fall). Komplett neu generierte *Typen* landen weiterhin nicht in `_allNamedTypesInSolution` (relevant nur für Interface-Implementierungs-Mapping) — das war für deinen Fall nicht nötig, lässt sich aber bei Bedarf ergänzen.
- Relationen, die *aus* generierten Methoden-Bodies herausgehen, werden nicht analysiert (Roslyn liefert für generierte Trees kein `solution.GetDocument`). Für „Methode ruft generiertes Property" — deinen Fall — ist das irrelevant.

Soll ich die Doku unter `Documentation/` (z.B. command-line-arguments oder ein README zu den Settings) noch um die neue Option ergänzen?



# Properties

Das ist eine reine Machbarkeitseinschätzung — ich implementiere nichts, sondern untersuche den Code, um die Tragweite fundiert einzuschätzen. Ich schaue mir die Stellen an, die Properties berühren.

Lass mich die zentralen Stellen anschauen: das `CodeElementType`-Enum, den `MethodBodyWalker` (Zugriffs-Kontext get/set), und wie Zyklen/Persistence Properties behandeln.

Ich habe die relevanten Stellen durchgesehen und kann eine fundierte Einschätzung geben. Kurzantwort vorweg: **Machbar, sinnvoll, aber eine mittelgroße Sache** — und der Aufwand liegt weniger in der Parser-Logik als im „Rattenschwanz" (Persistence, UI, vor allem die Approval-Tests).

## 1. Das Problem ist real — meine Meinung: lohnt sich

Das Zusammenfassen von Getter und Setter zu einem Knoten ist eine **Über-Approximation**. Wenn Typ A nur den Getter von `B.Prop` liest und Typ B nur den Setter von `A.Other` schreibt, entstehen am gemergten Property-Knoten Kanten, die in Wirklichkeit nie gemeinsam auftreten → **Phantom-Zyklus**. Das Aufsplitten ist die *prinzipiell korrekte* Lösung: Es kann nur falsche Zyklen entfernen, nie echte verstecken (Splitten ist strikt präziser als Mergen). Insofern: ja, ich halte das für eine gute, saubere Verbesserung der Analysequalität.

## 2. Der einzige wirklich *neue* technische Kern: get/set-Zugriffserkennung

Das ist der Knackpunkt. Roslyn liefert bei einem Property-Zugriff über `GetSymbolInfo` immer nur das `IPropertySymbol` — **nicht** „get oder set". Die Richtung muss aus dem **Syntax-Kontext** abgeleitet werden. Genau an zwei Stellen ([RelationshipAnalyzer.cs:222](https://claude.ai/epitaxy/CodeParser/Parser/RelationshipAnalyzer.cs:222) `AnalyzeIdentifier` und [RelationshipAnalyzer.cs:280](https://claude.ai/epitaxy/CodeParser/Parser/RelationshipAnalyzer.cs:280) `AnalyzeMemberAccess`) bräuchte es einen Helfer `DeterminePropertyAccessKind(node)`:

| Kontext                                                      | Zugriff           |
| ------------------------------------------------------------ | ----------------- |
| `x = Prop`, `f(Prop)`, `return Prop`, `if(Prop)`             | **get**           |
| `Prop = x` (einfache Zuweisung, auch Objekt-Initializer `new(){Prop=…}`) | **set**           |
| `Prop += 1`, `Prop++`, `--Prop` (Compound/Inkrement)         | **get + set**     |
| `f(ref Prop)` / `f(out Prop)`                                | **get+set / set** |

Das ist gut abgrenzbar (rein syntaktisch über den `Parent` des Ausdrucks), aber man muss die Compound-/ref-Fälle sauber treffen, sonst verschiebt man nur das Korrektheitsproblem. Das ist die einzige Stelle mit echtem Neudenken — alles andere ist Verkabelung.

## 3. Datenmodell-Empfehlung: Accessoren als **Kindknoten**, nicht Property ersetzen

Mein klarer Rat: Den Property-Knoten als **Container** behalten und get/set als Kinder darunterhängen (analog zu Methoden in einer Mini-Klasse), statt das Property durch zwei Leaf-Knoten zu ersetzen. Vorteile:

- **Grobgranulare Sichten bleiben intakt** (Modul-/Typ-Ebene aggregiert über die Property; deine `ModuleLevelApprovalTests` ändern sich kaum).
- **Persistence bleibt abwärtskompatibel** (neuer Enum-Wert *am Ende* + neue Kindelemente — alte Projekte laden weiter).
- **Roslyn liefert das Mapping gratis:** `IPropertySymbol.GetMethod`/`SetMethod` sind `IMethodSymbol` mit stabilen Namen `get_P`/`set_P`. Deren `Key()` ist bereits eindeutig — in Phase 1 mappt man `GetMethod.Key() → P_get` und `SetMethod.Key() → P_set`, in Phase 2 routet man nach Zugriffsart auf das passende Kind. Kein neuer Identitätsmechanismus nötig.

Dazu ein neuer `CodeElementType.PropertyAccessor` (am Enum-Ende: [CodeElementType.cs](https://claude.ai/epitaxy/CodeGraph/Graph/CodeElementType.cs)) statt `Method` wiederzuverwenden — damit Icon/Legende/optionales Einklappen sauber unterscheidbar bleiben.

## 4. Blast Radius (was angefasst werden muss)

**Parser (Kern, überschaubar):**

- [HierarchyAnalyzer.cs:257](https://claude.ai/epitaxy/CodeParser/Parser/HierarchyAnalyzer.cs:257) — `PropertyDeclaration`/`IndexerDeclaration`: Container + Accessor-Kinder anlegen, Accessor-Symbole mappen. Achtung: Record-Positional-Properties & Auto-Properties haben **keine** Accessor-Syntax → Kinder müssen symbolbasiert (über `GetMethod`/`SetMethod`) erzeugt werden, nicht syntaxbasiert.
- [RelationshipAnalyzer.cs](https://claude.ai/epitaxy/CodeParser/Parser/RelationshipAnalyzer.cs): die zwei Auflöse-Stellen (Zugriffsart-Routing), plus `AnalyzePropertyBody` ([:1218](https://claude.ai/epitaxy/CodeParser/Parser/RelationshipAnalyzer.cs:1218)) muss Getter-Body → `P_get`, Setter-Body → `P_set` als Quelle führen (die Accessor-Schleife trennt `get`/`set` ohnehin schon). Typ-/Override-/Implements-Kanten sinnvoll zuordnen.

**Domain (CodeGraph):**

- [RelationshipClassifier.cs:29](https://claude.ai/epitaxy/CodeGraph/Algorithms/Cycles/RelationshipClassifier.cs:29) — die `Property→Property Implements`-Sonderregel auf Accessoren erweitern.
- [CodeGraphExplorer.cs:286](https://claude.ai/epitaxy/CodeGraph/Exploration/CodeGraphExplorer.cs:286) — `is Method or Property` um den Accessor-Typ ergänzen (Call-Chain-Traversierung).
- Exporter ([PlantUmlExport.cs](https://claude.ai/epitaxy/CodeGraph/Export/PlantUmlExport.cs) etc.) und [ColorDefinitions.cs](https://claude.ai/epitaxy/CodeGraph/Colors/ColorDefinitions.cs) — meist generisch, brauchen je einen Fall/Eintrag.

**UI (mechanisch, kleine Cases):**

- [CodeElementIconMapper.cs:28](https://claude.ai/epitaxy/CSharpCodeAnalyst/Shared/Messages/CodeElementIconMapper.cs:28) (fällt sonst auf `Other` zurück), [TreeItemViewModel.cs](https://claude.ai/epitaxy/CSharpCodeAnalyst/Features/Tree/TreeItemViewModel.cs), [GraphHideFilter.cs](https://claude.ai/epitaxy/CSharpCodeAnalyst/Features/Graph/Filtering/GraphHideFilter.cs), [CodeElementNaming.cs](https://claude.ai/epitaxy/CSharpCodeAnalyst/Features/Refactoring/CodeElementNaming.cs), [LegendDialog.xaml.cs](https://claude.ai/epitaxy/CSharpCodeAnalyst/Features/Help/LegendDialog.xaml.cs).
- Persistence-DTO [SerializableCodeElement.cs](https://claude.ai/epitaxy/CSharpCodeAnalyst/Persistence/Dto/SerializableCodeElement.cs) — keine Strukturänderung, nur neuer Enum-Wert.

**Tests (der größte mechanische Posten):**

- Die `ApprovalTests/` mit den großen `HashSet<string>`-Literalen ändern sich für **jede** Fixture mit Properties spürbar. Das ist viel Diff, aber dank `DumpRelationships`/`DumpCodeElements` rein mechanisch.

## 5. Was NICHT betroffen ist — deine Annahme stimmt

Du liegst richtig: Der **Zyklen-Algorithmus selbst** (SCC/Tarjan) ist vollständig generisch über `CodeElement`/`Relationship` und braucht **null** Änderung. Nur der `RelationshipClassifier` (Filter, welche Kanten zählen) hat property-spezifische Sonderfälle. Präzisierung: Die Zyklen*suche* ändert sich nicht, die Zyklen*ergebnisse* werden besser (genau das Ziel).

## 6. Empfehlung zum Vorgehen

1. **Hinter eine Parser-Option** legen (genau wie gerade `IncludeGeneratedCode`), z.B. `SplitPropertyAccessors`. Damit bleibt das alte Verhalten Default, die Approval-Test-Churn ist opt-in, und du kannst Zyklen-Ergebnisse direkt A/B-vergleichen.
2. **Accessor-Kinder unter Property-Container** (siehe §3) + neuer `CodeElementType.PropertyAccessor`.
3. Zuerst die **Zugriffsart-Erkennung** als isolierten, gut getesteten Helfer bauen (das ist das Risiko), dann Hierarchie, dann Routing.
4. Events (`add`/`remove`) bewusst **außen vor** lassen — strukturell analog, aber separater Scope.

**Aufwandsschätzung grob:** Parser-Logik klein–mittel (~1–2 Tage inkl. Edge-Cases), Domain/UI klein (~0,5–1 Tag), Approval-Test-Neuabnahme mittel und nervig, aber risikoarm. Gesamt eine überschaubare, aber „echte" Story — kein Wochenend-Hack, aber auch nichts Architektur-Erschütterndes, gerade weil der Graph schon generisch ist.

**Offene Designfragen**, die ich dir zurückspiele:

- Soll der Property-Container selbst noch eingehende Kanten tragen dürfen (Fallback, wenn Zugriffsart unklar), oder strikt nur die Accessoren?
- Auto-Properties: get/set-Kinder trotzdem anlegen (Konsistenz) — ja, würde ich sagen, sonst gibt's kein Ziel für externe Lese-/Schreibzugriffe.

Soll ich einen konkreten Implementierungsplan (Schritt-für-Schritt, mit der `SplitPropertyAccessors`-Option) ausarbeiten — oder direkt einen Prototyp der Zugriffsart-Erkennung als Proof-of-Concept bauen?



Ich folge dem Plan und arbeite in kleinen, validierbaren Schritten.

**Schritt 1: Der isolierte, gut testbare Kern — die get/set-Zugriffsart-Erkennung.** Das ist das einzige Stück mit echtem Risiko, deshalb baue ich es zuerst eigenständig samt Unit-Tests.

Zunächst prüfe ich die Test-Projektstruktur und ob `internal`-Typen aus CodeParser sichtbar sind.

`InternalsVisibleTo("Tests")` ist gesetzt — der Helfer kann `internal` bleiben und direkt getestet werden. Ich schaue mir kurz den Stil eines bestehenden Unit-Tests an.

Jetzt erstelle ich den Klassifizierer. Kernidee: In C# kann ein Property **nicht** per `ref`/`out` übergeben werden (CS0206), daher sind die einzigen Schreib-Kontexte Zuweisungsziele und `++`/`--`. Das macht den Helfer angenehm klein und robust.

Jetzt die Unit-Tests für den Klassifizierer — rein syntaktisch, deckt die kritischen Fälle ab:

Der Test selbst war falsch konstruiert: in `Prop.Field` ist `Prop` ein `IdentifierNameSyntax` (der Empfänger), kein Member-Access-Name. Genau das ist der reale Fall (das Property als Empfänger eines weiteren Zugriffs). Ich korrigiere den Test:

✅ **Schritt 1 abgeschlossen und grün (13/13 Tests).** Der risikoreichste Teil — die get/set-Erkennung — steht isoliert und getestet.

## Was gebaut wurde

- [PropertyAccessClassifier.cs](https://claude.ai/epitaxy/CodeParser/Parser/PropertyAccessClassifier.cs) — `internal` Helfer + `PropertyAccessKind { Read, Write, ReadWrite }`. Rein syntaktisch, kein SemanticModel nötig.
- [PropertyAccessClassifierTests.cs](https://claude.ai/epitaxy/Tests/UnitTests/Parser/PropertyAccessClassifierTests.cs) — 13 Fälle.

**Die zentrale Vereinfachung** (bitte kurz gegenchecken, darauf baut alles auf): In C# kann ein Property nicht per `ref`/`out` übergeben werden (CS0206). Damit gibt es nur drei Schreib-Kontexte, alles andere ist Lesen:

| Code                                                         | Ergebnis    |
| ------------------------------------------------------------ | ----------- |
| `x = Prop`, `M(Prop)`, `return Prop`, `Prop.Field = 1` (Prop als Empfänger) | `Read`      |
| `Prop = 1`, `(Prop) = 1`, `this.Prop = 1`, `new C { Prop = 1 }` | `Write`     |
| `Prop += 1`, `Prop ??= 1`, `Prop++`, `--Prop`                | `ReadWrite` |

## Geplante nächste Schritte (in dieser Reihenfolge)

1. **Domänen-Typ + Option:** `CodeElementType.PropertyAccessor` ergänzen und die Parser-Option `SplitPropertyAccessors` einführen (noch ohne Wirkung).
2. **Phase 1 (Hierarchie):** Bei aktiver Option Property als Container behalten und get/set als Kindknoten anlegen (symbolbasiert über `GetMethod`/`SetMethod`, damit auch Auto-Properties/Records funktionieren), Keys mappen. → **Erster sichtbarer Validierungspunkt:** Die get/set-Knoten erscheinen im Baum.
3. **Phase 2 (Relationen):** Accessor-Bodies als Quelle auf das richtige Kind routen; Property-Zugriffe per Klassifizierer auf get/set-Kind(er) routen. → **Zweiter Validierungspunkt:** Phantom-Zyklen verschwinden.
4. **Verkabelung & Sichten:** AppSettings/Dialog, `RelationshipClassifier`, `CodeGraphExplorer`, Icon/Legende.

Passt die Semantik des Klassifizierers für dich? Wenn ja, mache ich mit Schritt 2+3 weiter, sodass du die neuen Knoten im Baum begutachten kannst.



Sehr gute Frage — die geht an den Kern, warum der syntaktische Weg überhaupt nötig ist. Kurz: **Nein, der Dependency-Lookup landet nicht auf den Accessor-Methoden.** Lass mich das genau erklären.

## Was Roslyn beim Zugriff zurückgibt

Wenn im Code `obj.Prop` oder `Prop = x` steht und wir `semanticModel.GetSymbolInfo(node)` aufrufen, bekommen wir das **`IPropertySymbol`** zurück — *nicht* das `IMethodSymbol` des Getters oder Setters.

Roslyn bindet einen Property-Ausdruck immer an das Property **als Einheit**. Die Information „hier wird get_Prop bzw. set_Prop aufgerufen" steckt **nicht** im gebundenen Symbol. Die Accessor-Methoden (`MethodKind.PropertyGet`/`PropertySet`) existieren zwar, sind aber nur erreichbar über:

```
propertySymbol.GetMethod   // IMethodSymbol get_Prop
propertySymbol.SetMethod   // IMethodSymbol set_Prop
```

…und welcher der beiden bei einem konkreten Zugriff „dran" ist, sagt Roslyn dir **an der Verwendungsstelle nicht**. Das ist genau die Lücke, die der Klassifizierer füllt.

Anders gesagt: Die Aufteilung in zwei Methoden passiert erst bei der **IL-Erzeugung** (Compiler-Backend). Auf der **semantischen Modell-Ebene**, auf der wir arbeiten, ist ein Property-Zugriff ein Property-Zugriff — die get/set-Auswahl ist implizit und ergibt sich nur aus der **Position** im Ausdruck.

## Gibt es einen semantischen Shortcut?

Nicht wirklich. Die Alternativen wären:

- **`GetSymbolInfo`** → liefert das Property (kein get/set). ❌
- **IOperation-API** (`IPropertyReferenceOperation` + Eltern-Operation wie `ISimpleAssignmentOperation.Target`) → würde funktionieren, ist aber dieselbe „schau auf den Kontext"-Logik, nur schwergewichtiger und mit eigener API-Lernkurve. Kein Gewinn.
- **`AnalyzeDataFlow`** → für diese Frage überdimensioniert.

Der syntaktische Weg ist hier tatsächlich der **pragmatische Standard**.

## Und: so komplex ist der Syntaxteil gar nicht

Deine Sorge („Syntaxbaum parsen ist recht komplex") ist berechtigt im Allgemeinen — aber für *diesen* Fall fällt die Komplexität dank CS0206 (ein Property kann nicht per `ref`/`out` übergeben werden) fast komplett weg. Es bleiben **genau drei** Schreib-Muster, der Rest ist Lesen:

```csharp
// Der ganze Klassifizierer ist im Kern dieser switch:
switch (node.Parent)
{
    case AssignmentExpressionSyntax a when a.Left == node:
        return a.IsKind(SimpleAssignment) ? Write : ReadWrite;   // Prop = x  /  Prop += x
    case PrefixUnaryExpressionSyntax  when ++/--: return ReadWrite;  // ++Prop
    case PostfixUnaryExpressionSyntax when ++/--: return ReadWrite;  // Prop++
    default: return Read;                                            // alles andere
}
```

Wir parsen also nicht den ganzen Baum — wir schauen nur **einen Knoten nach oben** (Parent) und prüfen drei Fälle. Genau deshalb habe ich diesen Teil zuerst gebaut und mit 13 Fällen abgesichert: Es ist das einzige Stück mit Denk-Risiko, und es ist klein und isoliert.

## Wo die Accessor-Methoden trotzdem ins Spiel kommen

In **Phase 1**: Dort nutzen wir `propertySymbol.GetMethod`/`SetMethod`, um die beiden Kindknoten zu erzeugen und ihre `Key()` zu mappen. In **Phase 2** läuft es dann so:

```
obj.Prop = x
   │
   ├─ GetSymbolInfo  → IPropertySymbol  (welches Property?)
   └─ Classify(node) → Write            (get oder set?)
        │
        └─ Lookup propertySymbol.SetMethod.Key()  → Knoten "Prop.set"
```

Erst die **Kombination** aus semantischer Auflösung (welches Property) und syntaktischer Klassifikation (welcher Accessor) gibt uns die richtige Kante. Keines von beiden allein reicht.

Soll ich das kurz **empirisch belegen** — ein kleiner Test, der einen Property-Zugriff kompiliert und ausgibt, dass `GetSymbolInfo` ein `IPropertySymbol` (und kein `IMethodSymbol`) liefert? Dann siehst du es schwarz auf weiß, bevor wir mit Phase 1 weitermachen.



Verstanden, dann weiter. Ich schaue kurz, wie Knoten im Graph beschriftet werden (Name vs. FullName), damit die get/set-Knoten gut lesbar heißen.

Knoten-Labels nutzen `element.Name`. Damit get/set auch in der flachen Graph-Ansicht eindeutig sind, benenne ich sie nach der Roslyn-Konvention `get_Prop`/`set_Prop` (das siehst du in Rider genauso).

**Schritt 2 — Domänen-Typ + Parser-Option.** Zuerst das Enum:

**Schritt 3 — Phase 1: get/set als Kindknoten anlegen.** Ich hänge nach dem Erzeugen des Property-Elements die Accessor-Kinder an (symbolbasiert über `GetMethod`/`SetMethod`, damit Auto-Properties, Indexer und Records mitlaufen).

Jetzt die Helfer-Methoden. Ich füge sie vor `WarnIfCodeElementHasMultipleSymbols` ein:

**Phase 2 (Source-Seite): Accessor-Bodies auf das jeweilige Kind-Element routen.** Damit kommen die ausgehenden Abhängigkeiten korrekt von `get_Prop`/`set_Prop` statt vom Property-Container:

Jetzt baue ich, um den Parser-Teil zu verifizieren, und verkable parallel die Option in die Settings/UI, damit du sie in der App einschalten und den Baum begutachten kannst.

Jetzt die Verkabelung durch Importer → ParserConfig und MainViewModel/ConsoleValidationCommand:

Jetzt die Dialog-Checkbox + Strings:

Damit die neuen Knoten ein sinnvolles Icon bekommen (statt „Other"), mappe ich `PropertyAccessor` auf das Property-Icon:

Jetzt der Build über die ganze Solution:

Das sind WPF-XAML-Build-Artefakte (`.g.cs`), die nicht regeneriert wurden — ein bekannter Inkrementell-Build-Glitch (verschärft durch `-clp:ErrorsOnly`, das die Markup-Compile-Phase stört). Ich baue normal neu:

Build grün — der vorige Fehler war wirklich nur der Logger-Glitch. Jetzt will ich das Verhalten mit aktiver Option automatisiert prüfen. Ich schaue, wie die Tests einen Graph aus Quellcode bauen.

Der Parser läuft über eine echte `.sln` (MSBuildWorkspace). Für eine automatisierte End-to-End-Prüfung mit aktiver Option schreibe ich einen eigenen Integrationstest, der TestSuite einmal mit `SplitPropertyAccessors=true` parst. Zuerst prüfe ich, ob der MSBuild-Locator doppelt initialisiert werden darf.

Der Locator-Init ist try/catch-geschützt; ich umschließe meinen Aufruf zusätzlich defensiv. Ich prüfe kurz, ob TestSuite Properties mit echten Getter-Bodies hat, um die Source-Routing-Prüfung konkret zu machen.

Ideal — `Facade.Value` hat einen Getter-Body, der `_repository.Compute()` aufruft. Damit kann ich das Source-Routing konkret prüfen: Die `Calls`-Kante muss von `get_Value` ausgehen, nicht vom Property-Container. Ich schreibe den Integrationstest:

Namespackollision: mein Test liegt im Namespace `...UnitTests.Parser`, daher kollidiert `Parser`. Ich qualifiziere die Klasse voll:

Der erste Test (Knoten existieren, korrekt geformt) ist grün. Die anderen scheitern an meiner geratenen `FullName` — vermutlich ist der Assembly-Name vorangestellt. Ich prüfe das tatsächliche Format.

Klar — der `FullName` enthält Assembly + die eingefügte `global`-Namespace-Ebene: `FollowHeuristic.global.FollowHeuristic.PropertyChain.…`. Ich korrigiere die Pfade im Test:

✅ **Schritte 2 + 3 abgeschlossen und verifiziert** (3/3 Integrationstests grün, parst echte TestSuite mit aktiver Option).

## Was jetzt funktioniert

Die Option **`SplitPropertyAccessors`** ist durchverkabelt bis in den Settings-Dialog (Tab *Application Settings* → „Split property getter/setter"). Bei Aktivierung:

- **Phase 1:** Jedes Property/Indexer bekommt `get_Prop`/`set_Prop` als **Kindknoten** (`CodeElementType.PropertyAccessor`), symbolbasiert — also auch Auto-Properties, Indexer und Record-Properties. Der Property-Knoten bleibt als Container erhalten.
- **Phase 2 (Source-Seite):** Accessor-Bodies werden dem richtigen Kind zugeordnet. Verifiziert am konkreten Fall:

```
Facade.Value { get { return _repository.Compute(); } }
```

→ die `Calls`-Kante zu `Repository.Compute` geht jetzt von **`get_Value`** aus — **nicht mehr** vom Property-Container `Value`.

**Validierungspunkt für dich:** Wenn du die Option im Dialog einschaltest und eine Solution neu lädst, erscheinen die `get_`/`set_`-Knoten im Baum unter dem jeweiligen Property, mit eigenen ausgehenden Abhängigkeiten. (Default ist aus → unverändertes Verhalten.)

## Was noch fehlt (Schritt 4 — der eigentliche Zyklen-Fix)

Aktuell ist nur die **Source-Seite** gesplittet. Die **Target-Seite** (eingehende Zugriffe) zeigt noch auf den Container:

- `var x = facade.Value;` (Lesen) und `facade.Value = …;` (Schreiben) landen beide noch auf `Value`, nicht auf `get_Value`/`set_Value`.
- Erst wenn ich hier den `PropertyAccessClassifier` einhänge (read→get, write→set, read-write→beide), verschwinden die Phantom-Zyklen.

Dabei mache ich dann auch:

- [CodeGraphExplorer.cs:286](https://claude.ai/epitaxy/CodeGraph/Exploration/CodeGraphExplorer.cs:286) — `PropertyAccessor` in die Call-Chain-Traversierung aufnehmen (sonst bricht „Follow Calls" an Accessoren ab).
- [RelationshipClassifier.cs](https://claude.ai/epitaxy/CodeGraph/Algorithms/Cycles/RelationshipClassifier.cs) — die Property→Property-Sonderregeln auf Accessoren ausweiten.

Möchtest du den jetzigen Stand erst in Rider/im Baum begutachten, oder soll ich direkt mit Schritt 4 (Target-Routing + Zyklen-Fix) weitermachen?



**Schritt 4 — Target-Routing + Zyklen-Fix.** Ich hänge den `PropertyAccessClassifier` an die beiden Auflöse-Stellen und route eingehende Zugriffe auf `get_`/`set_`. Zuerst die zentrale Routing-Logik im RelationshipAnalyzer:

Jetzt ändere ich beide Auflöse-Stellen, sodass sie über einen gemeinsamen Helfer routen:

Jetzt der Routing-Helfer. Ich platziere ihn neben `AddRelationshipWithFallbackToContainingType`:

Jetzt der `CodeGraphExplorer` (Call-Chain-Traversierung) und der `RelationshipClassifier`. Zuerst lese ich den Explorer-Kontext genauer:

`GetMethodContainer` (Zeile 677) läuft generisch über `Parent` hoch — für einen `PropertyAccessor` landet er korrekt bei der umschließenden Klasse. Ich muss nur die Call-Chain-Bedingung um den neuen Typ erweitern:

Der `RelationshipClassifier` braucht hier tatsächlich **keine** Änderung: Override/Implements bleiben bewusst auf dem Property-Container (Property→Property, bereits zyklus-ausgeschlossen); die Accessoren tragen nur `Calls`/`Uses`. Der Zyklen-Fix entsteht allein dadurch, dass Lese-/Schreibkanten jetzt auf getrennten Knoten landen.

Ich baue und führe die Property- und Zyklen-Tests aus:

Build grün. Jetzt erweitere ich den Integrationstest um die **Target-Seite**: `Client.Consume` liest `facade.Value` — das muss jetzt eine `Calls`-Kante auf `get_Value` erzeugen (statt auf den Container).

Ich suche noch einen konkreten Schreib-Zugriff in TestSuite, um die Setter-Seite ebenfalls abzudecken:

Perfekt — `ModelA.ModelCPropertyOfModelA` hat get **und** set, mit je einer eigenen Lese-/Schreib-Methode. Ideal für beide Seiten. Ich prüfe das FullName-Präfix für ModuleLevel1:

`ModuleLevel1.global.ModuleLevel1.Model.ModelA…` — sauberes Präfix. Ich füge einen Test hinzu, der Lese- und Schreibzugriff am selben Property eindeutig trennt:

Alle 5 Integrationstests grün — Target-Routing funktioniert (Lesen→`get_`, Schreiben→`set_`, Container trägt keine Zugriffskanten mehr). Wichtig: die Tests laufen im Debug-Build, d.h. die `CodeGraphPlausibilityChecks` haben den Graph mit Accessor-Knoten ohne Beanstandung akzeptiert.

Zur Sicherheit prüfe ich den Default-Pfad (Option aus) über die Cycles-/Exploration-Unit-Tests:

Alle 46 Tests grün — Default-Pfad unverändert. **Schritt 4 ist damit funktional vollständig.** Bevor ich abschließe: Damit beim Laden in der App nichts abstürzt oder seltsam aussieht, prüfe ich kurz die Stellen mit per-Typ-Logik (Farben, Namensgebung, Filter, Legende) auf den neuen Enum-Wert.

Die für das **Laden/Rendern** kritische Stelle ist `ColorDefinitions` (jeder Knoten bekommt beim Rendern eine Farbe). Ich prüfe, ob dort ein Default-Arm existiert:

`ColorDefinitions` hat einen Default-Arm (kein Crash), aber `PropertyAccessor` würde weiß. Ich gebe ihm die Property-Farbe für visuelle Konsistenz:

Ich prüfe noch die Render-Pfade (MSAGL + WebGraph), ob dort Typ-Switches ohne Default-Arm abstürzen könnten:

Die Render-Pfade sind safe (WebGraph nutzt `ColorDefinitions` mit Default + `element.Name`; kein abstürzender Typ-Switch). Die übrigen per-Typ-Switches (PlantUML-Export, Refactoring) liegen auf user-getriggerten Pfaden, nicht beim Laden/Rendern — die hebe ich mir für die Politur (Schritt 5) auf.

Finaler Build:

✅ **Schritt 4 abgeschlossen und verifiziert** (5/5 Integrationstests + 46 Cycles/Exploration-Tests grün, Build sauber).

## Was jetzt funktional vollständig ist

Bei aktivierter Option `SplitPropertyAccessors` ist der Property-Split **end-to-end** wirksam:

| Aspekt           | Verhalten                                                    |
| ---------------- | ------------------------------------------------------------ |
| **Phase 1**      | `get_Prop`/`set_Prop` als Kindknoten unter dem Property-Container |
| **Source-Seite** | Getter-/Setter-Body → ausgehende Kanten vom jeweiligen Accessor |
| **Target-Seite** | Lesen → `get_`, Schreiben → `set_`, Read-Modify-Write (`+=`,`++`) → **beide** |
| **Explorer**     | „Follow Calls" läuft durch Accessoren ([CodeGraphExplorer.cs:287](https://claude.ai/epitaxy/CodeGraph/Exploration/CodeGraphExplorer.cs:287)) |
| **Rendering**    | Eigene Farbe/Icon, kein Crash auf dem neuen Typ              |

Konkret per Test belegt (echte TestSuite-Parse, Debug-Build inkl. Plausibilitätschecks):

- `ModelA.AccessToPropertiesSetter` (`Prop = new ModelC()`) → **nur** `set_ModelCPropertyOfModelA`
- `ModelA.AccessToPropertiesGetter` (`var x = Prop`) → **nur** `get_ModelCPropertyOfModelA`
- In keinem Fall mehr eine Kante auf den Property-Container

**Das ist der Kern des Zyklen-Fixes:** Lese- und Schreibkanten landen jetzt auf getrennten Knoten — ein Phantom-Zyklus, der nur durch das Mergen von get+set entstand, kann nicht mehr gebildet werden.

## Validierungspunkt für dich

Option im Dialog einschalten → Solution neu laden → im Baum erscheinen `get_`/`set_`-Knoten; im Graph siehst du, dass z.B. eine nur-lesende und eine nur-schreibende Klasse nicht mehr über dasselbe Property gekoppelt sind. Default (aus) ist byte-identisch zum alten Verhalten.

## Bewusst offen gelassen (Schritt 5 — Politur, optional)

1. **Override/Implements** bleiben auf dem Property-Container (Property→Property). Für Interface-Properties wird also keine Accessor-Ebenen-`Implements`-Kante erzeugt → „Follow abstractions" springt bei Accessoren nicht auf den Interface-Accessor. Zyklustechnisch irrelevant (ohnehin ausgeschlossen), aber eine Genauigkeitslücke beim Explorer.
2. **PlantUML-Export & Refactoring-Service** habe ich für den neuen Typ noch nicht geprüft (user-getriggerte Pfade, kein Crash-Risiko beim Laden).
3. **Legende** ([LegendDialog](https://claude.ai/epitaxy/CSharpCodeAnalyst/Features/Help/LegendDialog.xaml.cs)) zeigt den Accessor-Typ noch nicht separat.

Möchtest du den Stand erst in Rider begutachten, oder soll ich mit der Politur (Punkt 1–3) weitermachen? Mein Vorschlag: erst du validierst visuell, dann entscheiden wir, ob Punkt 1 (Accessor-Level Implements/Overrides) den Aufwand wert ist.

