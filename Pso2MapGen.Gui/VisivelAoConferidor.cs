using System.Runtime.CompilerServices;

// O `Pso2MapGen.ConfereDpi` precisa de construir a `DialogoBits`, que e' interna, para a medir
// a 200%.  E' o unico motivo desta linha; nada mais ve' estes tipos.
[assembly: InternalsVisibleTo("Pso2MapGen.ConfereDpi")]
