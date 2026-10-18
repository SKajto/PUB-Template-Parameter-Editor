import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { parseDeclarations } from '../../src/core/declaration-parser';

const template = (variables: string): string =>
  `<template><dataset><variables>${variables}</variables></dataset></template>`;

const declaration = (
  options: {
    name?: string;
    access?: string;
    defaultValue?: string;
    description?: string;
  } = {},
): string => {
  const name = options.name ?? 'sample';
  const access = options.access ?? 'external';
  const defaultValue = options.defaultValue ?? '';
  const description = options.description ?? '';
  return `<variable><feature tag="definition"><property name="name" value="${name}"/><property name="description"><expression type="constant">${description}</expression></property><property name="type" value="user"/><property name="access" value="${access}"/><property name="default" value="${defaultValue}"/><property name="central variable" value=""/></feature></variable>`;
};

describe('parseDeclarations', () => {
  it('reads declaration fields as text and preserves empty defaults', () => {
    const xml = template(
      declaration({
        name: 'displayName',
        defaultValue: 'alert(&quot;not executed&quot;); &amp; retained',
        description: 'Input &amp; label',
      }),
    );

    const [result] = parseDeclarations(xml);

    assert.equal(result.name, 'displayName');
    assert.equal(result.defaultValue, 'alert("not executed"); & retained');
    assert.equal(result.description, 'Input & label');
    assert.equal(result.access, 'external');
    assert.equal(result.xmlType, 'user');
    assert.deepEqual(result.readOnlyReasons, []);

    const [empty] = parseDeclarations(template(declaration()));
    assert.equal(empty.defaultValue, '');
    assert.equal(empty.description, '');
  });

  it('keeps declaration names case-sensitive and marks exact duplicates read-only', () => {
    const results = parseDeclarations(
      template(
        declaration({ name: 'same' }) +
          declaration({ name: 'same' }) +
          declaration({ name: 'Same' }),
      ),
    );

    assert.equal(results.length, 3);
    assert.ok(
      results[0].readOnlyReasons.some((reason) =>
        reason.includes('Duplicate declaration name'),
      ),
    );
    assert.ok(
      results[1].readOnlyReasons.some((reason) =>
        reason.includes('Duplicate declaration name'),
      ),
    );
    assert.deepEqual(results[2].readOnlyReasons, []);
  });

  it('marks unknown access and non-constant descriptions read-only', () => {
    const xml = template(
      `<variable><feature tag="definition"><property name="name" value="scriptLike"/><property name="description"><expression type="script">return 'text';</expression></property><property name="type" value="user"/><property name="access" value="shared"/><property name="default" value="42"/><property name="central variable" value=""/></feature></variable>`,
    );
    const [result] = parseDeclarations(xml);

    assert.equal(result.defaultValue, '42');
    assert.equal(result.description, null);
    assert.ok(
      result.readOnlyReasons.some((reason) => reason.includes('Access')),
    );
    assert.ok(
      result.readOnlyReasons.some((reason) =>
        reason.includes('constant expression'),
      ),
    );
  });

  it('rejects malformed XML, DTDs, and unsupported declaration scopes', () => {
    assert.throws(
      () => parseDeclarations('<template><broken></template>'),
      /Invalid XML/,
    );
    assert.throws(
      () =>
        parseDeclarations(
          '<!DOCTYPE template [<!ENTITY value "x">]><template/>',
        ),
      /DTD declarations are not supported/,
    );
    assert.throws(
      () =>
        parseDeclarations(
          '<template><dataset><variables/></dataset></template>',
        ),
      /No declarations found/,
    );
  });
});
