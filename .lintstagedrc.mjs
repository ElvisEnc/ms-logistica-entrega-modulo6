// Formats only the staged C# files before each commit (tab indentation rule).
// `dotnet format --include` silently ignores absolute paths, so make them relative.
import path from 'node:path';

const repoRoot = import.meta.dirname;
const quote = (value) => `"${value}"`;
const relative = (file) => quote(path.relative(repoRoot, file));

export default {
	'*.cs': (files) => `dotnet format whitespace . --folder --include ${files.map(relative).join(' ')}`,
};
