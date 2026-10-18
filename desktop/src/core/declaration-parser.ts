import { SaxesParser, type SaxesTag } from 'saxes';
import type { ParameterDeclaration } from '../shared/types';

interface MutableDeclaration {
  id: string;
  properties: Map<string, string | null>;
  propertyCounts: Map<string, number>;
  descriptionExpressionType: string | null;
  description: string;
  descriptionSeen: boolean;
  reasons: Set<string>;
}

const DECLARATION_FEATURE_PATH = 'template/dataset/variables/variable/feature';

function attribute(tag: SaxesTag, name: string): string | null {
  const value = tag.attributes[name];
  return typeof value === 'string' ? value : null;
}

function finishDeclaration(current: MutableDeclaration): ParameterDeclaration {
  const get = (name: string): string | null =>
    current.properties.get(name) ?? null;
  const counts = current.propertyCounts;

  for (const required of ['name', 'description', 'access', 'default', 'type']) {
    if ((counts.get(required) ?? 0) === 0) {
      current.reasons.add(`Missing ${required} property.`);
    } else if ((counts.get(required) ?? 0) > 1) {
      current.reasons.add(`Duplicate ${required} properties.`);
    }
  }

  const name = get('name');
  const access = get('access');
  const defaultValue = get('default');
  const xmlType = get('type');
  const description =
    current.descriptionSeen && current.descriptionExpressionType === 'constant'
      ? current.description
      : null;

  if (name === null)
    current.reasons.add('Name property has no value attribute.');
  if (defaultValue === null) {
    current.reasons.add('Default property has no value attribute.');
  }
  if (access !== 'internal' && access !== 'external') {
    current.reasons.add('Access must be internal or external.');
  }
  if (current.descriptionExpressionType !== 'constant') {
    current.reasons.add('Description is not a constant expression.');
  }

  return {
    id: current.id,
    name,
    description,
    access,
    defaultValue,
    xmlType,
    readOnlyReasons: [...current.reasons],
  };
}

function markDuplicateNames(declarations: ParameterDeclaration[]): void {
  const matching = new Map<string, ParameterDeclaration[]>();
  for (const declaration of declarations) {
    if (declaration.name === null) continue;
    const group = matching.get(declaration.name) ?? [];
    group.push(declaration);
    matching.set(declaration.name, group);
  }
  for (const [name, group] of matching) {
    if (group.length < 2) continue;
    for (const declaration of group) {
      declaration.readOnlyReasons.push(`Duplicate declaration name: ${name}.`);
    }
  }
}

/** Parse declarations from UTF-8 XML text without evaluating scripts or DTDs. */
export function parseDeclarations(xml: string): ParameterDeclaration[] {
  const declarations: ParameterDeclaration[] = [];
  const stack: string[] = [];
  let current: MutableDeclaration | null = null;
  let currentProperty: string | null = null;
  let capturingDescription = false;
  let descriptionExpressionDepth = 0;
  let rootName: string | null = null;
  let index = 0;

  const parser = new SaxesParser({ xmlns: false, fragment: false });
  parser.on('error', (error) => {
    throw new Error(`Invalid XML: ${error.message}`, { cause: error });
  });
  parser.on('doctype', () => {
    throw new Error('DTD declarations are not supported.');
  });
  parser.on('opentag', (tag) => {
    const path = [...stack, tag.name].join('/');
    if (stack.length === 0) rootName = tag.name;

    if (
      path === DECLARATION_FEATURE_PATH &&
      attribute(tag, 'tag') === 'definition'
    ) {
      current = {
        id: `declaration-${index}`,
        properties: new Map(),
        propertyCounts: new Map(),
        descriptionExpressionType: null,
        description: '',
        descriptionSeen: false,
        reasons: new Set(),
      };
      index += 1;
    } else if (
      current !== null &&
      path === `${DECLARATION_FEATURE_PATH}/property`
    ) {
      currentProperty = attribute(tag, 'name');
      if (currentProperty !== null) {
        const count = (current.propertyCounts.get(currentProperty) ?? 0) + 1;
        current.propertyCounts.set(currentProperty, count);
        if (count === 1) {
          current.properties.set(currentProperty, attribute(tag, 'value'));
        } else {
          current.reasons.add(`Duplicate ${currentProperty} properties.`);
        }
        if (currentProperty === 'description') {
          current.descriptionSeen = true;
        }
      }
    } else if (
      current !== null &&
      currentProperty === 'description' &&
      path === `${DECLARATION_FEATURE_PATH}/property/expression`
    ) {
      descriptionExpressionDepth += 1;
      current.descriptionExpressionType = attribute(tag, 'type');
      capturingDescription = current.descriptionExpressionType === 'constant';
    }

    stack.push(tag.name);
  });

  const appendDescription = (text: string): void => {
    if (current !== null && capturingDescription) current.description += text;
  };
  parser.on('text', appendDescription);
  parser.on('cdata', appendDescription);

  parser.on('closetag', () => {
    const path = stack.join('/');
    if (path === `${DECLARATION_FEATURE_PATH}/property/expression`) {
      descriptionExpressionDepth = Math.max(0, descriptionExpressionDepth - 1);
      if (descriptionExpressionDepth === 0) capturingDescription = false;
    } else if (path === `${DECLARATION_FEATURE_PATH}/property`) {
      currentProperty = null;
    } else if (path === DECLARATION_FEATURE_PATH && current !== null) {
      declarations.push(finishDeclaration(current));
      current = null;
      currentProperty = null;
      capturingDescription = false;
      descriptionExpressionDepth = 0;
    }
    stack.pop();
  });

  parser.write(xml).close();
  validateXmlEncoding(parser.xmlDecl?.encoding);
  if (rootName !== 'template') {
    throw new Error(
      `Expected <template> root; found ${rootName ?? 'no root element'}.`,
    );
  }
  if (declarations.length === 0) {
    throw new Error('No declarations found at the supported dataset path.');
  }

  markDuplicateNames(declarations);
  return declarations;
}

export function validateXmlEncoding(encoding: string | undefined): void {
  if (encoding && !/^utf-?8$/i.test(encoding)) {
    throw new Error(`Only UTF-8 template.xml is supported; found ${encoding}.`);
  }
}
