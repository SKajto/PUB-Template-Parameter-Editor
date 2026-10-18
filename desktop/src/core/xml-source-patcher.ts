import type {
  EditableField,
  EditableFieldCapabilities,
  ParameterDeclaration,
} from '../shared/types';
import { parseDeclarations } from './declaration-parser';

export type EditableXmlField = EditableField;

interface SourceSpan {
  start: number;
  end: number;
  quote?: '"' | "'";
}

interface DeclarationSpans {
  fields: Partial<Record<EditableXmlField, SourceSpan>>;
}

export interface XmlSourceModel {
  declarations: ParameterDeclaration[];
  editableFields: EditableFieldCapabilities[];
}

interface AttributeToken {
  value: string;
  valueSpan: SourceSpan;
}

interface XmlToken {
  name: string;
  start: number;
  end: number;
  closing: boolean;
  selfClosing: boolean;
  attributes: Map<string, AttributeToken>;
  special?: boolean;
}

const DECLARATION_PATH = 'template/dataset/variables/variable/feature';

function xmlError(message: string, offset: number): Error {
  return new Error(`Unsupported XML source at character ${offset}: ${message}`);
}

function readName(xml: string, offset: number): { name: string; end: number } {
  let end = offset;
  while (end < xml.length && !/[\s/>=]/u.test(xml[end])) end += 1;
  if (end === offset) throw xmlError('expected an XML name', offset);
  return { name: xml.slice(offset, end), end };
}

function skipSpace(xml: string, offset: number): number {
  let end = offset;
  while (end < xml.length && /\s/u.test(xml[end])) end += 1;
  return end;
}

function scanTag(xml: string, start: number): XmlToken {
  let cursor = start + 1;
  const closing = xml[cursor] === '/';
  if (closing) cursor += 1;
  const parsedName = readName(xml, cursor);
  cursor = parsedName.end;
  const attributes = new Map<string, AttributeToken>();

  if (closing) {
    cursor = skipSpace(xml, cursor);
    if (xml[cursor] !== '>') throw xmlError('malformed closing tag', cursor);
    return {
      name: parsedName.name,
      start,
      end: cursor + 1,
      closing: true,
      selfClosing: false,
      attributes,
    };
  }

  while (cursor < xml.length) {
    cursor = skipSpace(xml, cursor);
    if (xml[cursor] === '>') {
      return {
        name: parsedName.name,
        start,
        end: cursor + 1,
        closing: false,
        selfClosing: false,
        attributes,
      };
    }
    if (xml[cursor] === '/' && xml[cursor + 1] === '>') {
      return {
        name: parsedName.name,
        start,
        end: cursor + 2,
        closing: false,
        selfClosing: true,
        attributes,
      };
    }

    const parsedAttribute = readName(xml, cursor);
    cursor = skipSpace(xml, parsedAttribute.end);
    if (xml[cursor] !== '=')
      throw xmlError('expected = after an attribute', cursor);
    cursor = skipSpace(xml, cursor + 1);
    const quote = xml[cursor];
    if (quote !== '"' && quote !== "'") {
      throw xmlError('expected a quoted attribute value', cursor);
    }
    const valueStart = cursor + 1;
    const valueEnd = xml.indexOf(quote, valueStart);
    if (valueEnd < 0)
      throw xmlError('unterminated attribute value', valueStart);
    if (attributes.has(parsedAttribute.name)) {
      throw xmlError(`duplicate attribute ${parsedAttribute.name}`, cursor);
    }
    attributes.set(parsedAttribute.name, {
      value: xml.slice(valueStart, valueEnd),
      valueSpan: { start: valueStart, end: valueEnd, quote },
    });
    cursor = valueEnd + 1;
  }
  throw xmlError('unterminated start tag', start);
}

function scanTokens(xml: string): XmlToken[] {
  const tokens: XmlToken[] = [];
  let cursor = 0;
  while (cursor < xml.length) {
    const start = xml.indexOf('<', cursor);
    if (start < 0) break;
    if (xml.startsWith('<!--', start)) {
      const end = xml.indexOf('-->', start + 4);
      if (end < 0) throw xmlError('unterminated comment', start);
      tokens.push({
        name: '',
        start,
        end: end + 3,
        closing: false,
        selfClosing: false,
        attributes: new Map(),
        special: true,
      });
      cursor = end + 3;
      continue;
    }
    if (xml.startsWith('<![CDATA[', start)) {
      const end = xml.indexOf(']]>', start + 9);
      if (end < 0) throw xmlError('unterminated CDATA section', start);
      tokens.push({
        name: '',
        start,
        end: end + 3,
        closing: false,
        selfClosing: false,
        attributes: new Map(),
        special: true,
      });
      cursor = end + 3;
      continue;
    }
    if (xml.startsWith('<?', start)) {
      const end = xml.indexOf('?>', start + 2);
      if (end < 0) throw xmlError('unterminated processing instruction', start);
      tokens.push({
        name: '',
        start,
        end: end + 2,
        closing: false,
        selfClosing: false,
        attributes: new Map(),
        special: true,
      });
      cursor = end + 2;
      continue;
    }
    if (xml.startsWith('<!', start)) {
      throw xmlError(
        'declarations are not supported for source editing',
        start,
      );
    }
    const token = scanTag(xml, start);
    tokens.push(token);
    cursor = token.end;
  }
  return tokens;
}

function spanForAttribute(
  token: XmlToken,
  attributeName: string,
): SourceSpan | undefined {
  return token.attributes.get(attributeName)?.valueSpan;
}

function collectDeclarationSpans(xml: string): DeclarationSpans[] {
  const spans: DeclarationSpans[] = [];
  const stack: string[] = [];
  let current: DeclarationSpans | null = null;
  let currentProperty: string | null = null;
  let descriptionStart: number | null = null;
  let descriptionComplex = false;

  for (const token of scanTokens(xml)) {
    if (token.special) {
      if (current !== null && descriptionStart !== null) {
        descriptionComplex = true;
      }
      continue;
    }
    if (token.closing) {
      const path = stack.join('/');
      if (path === `${DECLARATION_PATH}/property/expression`) {
        if (
          current !== null &&
          !descriptionComplex &&
          descriptionStart !== null
        ) {
          current.fields.description = {
            start: descriptionStart,
            end: token.start,
          };
        }
        descriptionStart = null;
      } else if (path === `${DECLARATION_PATH}/property`) {
        currentProperty = null;
      } else if (path === DECLARATION_PATH && current !== null) {
        spans.push(current);
        current = null;
      }
      stack.pop();
      continue;
    }

    const path = [...stack, token.name].join('/');
    if (
      path === DECLARATION_PATH &&
      token.attributes.get('tag')?.value === 'definition'
    ) {
      current = { fields: {} };
    } else if (current !== null && path === `${DECLARATION_PATH}/property`) {
      currentProperty = token.attributes.get('name')?.value ?? null;
      if (currentProperty === 'access' || currentProperty === 'default') {
        const field = currentProperty === 'default' ? 'defaultValue' : 'access';
        const span = spanForAttribute(token, 'value');
        if (span !== undefined) current.fields[field] = span;
      }
    } else if (
      current !== null &&
      currentProperty === 'description' &&
      path === `${DECLARATION_PATH}/property/expression`
    ) {
      if (token.attributes.get('type')?.value === 'constant') {
        descriptionStart = token.end;
        descriptionComplex = false;
        if (token.selfClosing) {
          current.fields.description = { start: token.end, end: token.end };
          descriptionStart = null;
        }
      }
    } else if (
      current !== null &&
      descriptionStart !== null &&
      path.startsWith(`${DECLARATION_PATH}/property/expression/`)
    ) {
      descriptionComplex = true;
    }

    if (token.selfClosing) {
      if (path === `${DECLARATION_PATH}/property`) currentProperty = null;
      if (path === DECLARATION_PATH && current !== null) {
        spans.push(current);
        current = null;
      }
    } else {
      stack.push(token.name);
    }
  }
  return spans;
}

function assertXmlCharacters(value: string): void {
  for (const character of value) {
    const codePoint = character.codePointAt(0)!;
    const valid =
      codePoint === 0x9 ||
      codePoint === 0xa ||
      codePoint === 0xd ||
      (codePoint >= 0x20 && codePoint <= 0xd7ff) ||
      (codePoint >= 0xe000 && codePoint <= 0xfffd) ||
      (codePoint >= 0x10000 && codePoint <= 0x10ffff);
    if (!valid)
      throw new Error('The proposed value contains an XML-invalid character.');
  }
}

function escapeAttribute(value: string, quote: '"' | "'"): string {
  assertXmlCharacters(value);
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll(quote, quote === '"' ? '&quot;' : '&apos;')
    .replaceAll('\t', '&#9;')
    .replaceAll('\n', '&#10;')
    .replaceAll('\r', '&#13;');
}

function escapeText(value: string): string {
  assertXmlCharacters(value);
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('\r', '&#13;');
}

function declarationValues(declaration: ParameterDeclaration): string[] {
  return [
    declaration.name ?? '',
    declaration.description ?? '',
    declaration.access ?? '',
    declaration.defaultValue ?? '',
    declaration.xmlType ?? '',
  ];
}

export function parseXmlSource(xml: string): XmlSourceModel {
  const declarations = parseDeclarations(xml);
  const sourceSpans = collectDeclarationSpans(xml);
  if (sourceSpans.length !== declarations.length) {
    throw new Error(
      'Could not map every declaration to its original XML source.',
    );
  }
  return {
    declarations,
    editableFields: sourceSpans.map(({ fields }) => ({
      access: fields.access !== undefined,
      defaultValue: fields.defaultValue !== undefined,
      description: fields.description !== undefined,
    })),
  };
}

/** Patch only supported field source spans; unchanged source bytes remain untouched. */
export function patchXmlSource(
  xml: string,
  edits: Array<{
    declarationId: string;
    field: EditableXmlField;
    value: string;
  }>,
): string {
  if (edits.length === 0) return xml;
  const before = parseDeclarations(xml);
  const sourceSpans = collectDeclarationSpans(xml);
  if (sourceSpans.length !== before.length) {
    throw new Error(
      'Could not map every declaration to its original XML source.',
    );
  }

  const replacements: Array<{ span: SourceSpan; value: string }> = [];
  const seen = new Set<string>();
  for (const edit of edits) {
    const index = Number(edit.declarationId.replace(/^declaration-/u, ''));
    if (
      !Number.isSafeInteger(index) ||
      before[index]?.id !== edit.declarationId
    ) {
      throw new Error(`Unknown declaration: ${edit.declarationId}`);
    }
    const key = `${index}:${edit.field}`;
    if (seen.has(key)) throw new Error(`Duplicate edit for ${key}.`);
    seen.add(key);
    if (
      edit.field === 'access' &&
      edit.value !== 'internal' &&
      edit.value !== 'external'
    ) {
      throw new Error('Access must be internal or external.');
    }
    const currentValue =
      edit.field === 'access'
        ? before[index].access
        : edit.field === 'defaultValue'
          ? before[index].defaultValue
          : before[index].description;
    if (currentValue === edit.value) continue;
    if (before[index].readOnlyReasons.length > 0) {
      throw new Error(`Declaration ${edit.declarationId} is read-only.`);
    }
    const span = sourceSpans[index].fields[edit.field];
    if (span === undefined) {
      throw new Error(`Field ${edit.field} has no supported source span.`);
    }
    replacements.push({
      span,
      value:
        edit.field === 'description'
          ? escapeText(edit.value)
          : escapeAttribute(edit.value, span.quote!),
    });
  }

  if (replacements.length === 0) return xml;

  replacements.sort((left, right) => right.span.start - left.span.start);
  let output = xml;
  let previousStart = Number.POSITIVE_INFINITY;
  for (const replacement of replacements) {
    if (replacement.span.end > previousStart) {
      throw new Error('Overlapping source edits are not supported.');
    }
    output =
      output.slice(0, replacement.span.start) +
      replacement.value +
      output.slice(replacement.span.end);
    previousStart = replacement.span.start;
  }

  const after = parseDeclarations(output);
  if (after.length !== before.length)
    throw new Error('Patched XML changed declaration count.');
  for (let index = 0; index < before.length; index += 1) {
    const expected = { ...before[index] };
    for (const edit of edits) {
      if (edit.declarationId !== before[index].id) continue;
      if (edit.field === 'access') expected.access = edit.value;
      if (edit.field === 'defaultValue') expected.defaultValue = edit.value;
      if (edit.field === 'description') expected.description = edit.value;
    }
    if (
      declarationValues(after[index]).some(
        (value, field) => value !== declarationValues(expected)[field],
      )
    ) {
      throw new Error('Patched XML changed an unrelated declaration field.');
    }
  }
  return output;
}
