import type { ParameterDeclaration } from '../shared/types';

function escapeRegex(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/gu, '\\$&');
}

function createMatcher(query: string, mode: 'name' | 'description'): RegExp {
  const expression = query.split('*').map(escapeRegex).join('.*');
  const source = `${mode === 'name' ? '^' : ''}${expression}`;
  return new RegExp(source, mode === 'name' ? 'su' : 'isu');
}

/** Names are case-sensitive prefix searches; '*' spans any text. Descriptions match anywhere, case-insensitively. */
export function matchesParameterSearch(
  declaration: ParameterDeclaration,
  rawQuery: string,
): boolean {
  const query = rawQuery.trim();
  if (query.length === 0) return true;
  const nameMatcher = createMatcher(query, 'name');
  if (declaration.name !== null && nameMatcher.test(declaration.name))
    return true;
  if (
    declaration.name !== null &&
    createMatcher(query, 'description').test(declaration.name)
  ) {
    return false;
  }
  const descriptionMatcher = createMatcher(query, 'description');
  return (
    declaration.description !== null &&
    descriptionMatcher.test(declaration.description)
  );
}
