import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { ParameterDeclaration } from '../../src/shared/types';
import { matchesParameterSearch } from '../../src/core/parameter-search';

function parameter(
  name: string,
  description: string | null = '',
): ParameterDeclaration {
  return {
    id: name,
    name,
    description,
    access: 'external',
    defaultValue: '',
    xmlType: 'user',
    readOnlyReasons: [],
  };
}

describe('parameter search', () => {
  it('matches names from the beginning rather than matching an interior substring', () => {
    assert.equal(
      matchesParameterSearch(parameter('param01Attribute'), 'param'),
      true,
    );
    assert.equal(
      matchesParameterSearch(parameter('_param01Attribute'), 'param'),
      false,
    );
    assert.equal(
      matchesParameterSearch(
        parameter('_param01Attribute', 'parameter settings'),
        'param',
      ),
      false,
    );
    assert.equal(
      matchesParameterSearch(parameter('prefix_param01Attribute'), 'param'),
      false,
    );
  });

  it('matches wildcard name searches case-sensitively from the start', () => {
    assert.equal(
      matchesParameterSearch(parameter('param01Attribute'), 'param*Attribute'),
      true,
    );
    assert.equal(
      matchesParameterSearch(parameter('param02Attribute'), 'param*Attribute'),
      true,
    );
    const numberedAttributeNames = Array.from(
      { length: 20 },
      (_, index) => `param${String(index + 1).padStart(2, '0')}Attributes`,
    );
    for (const name of numberedAttributeNames) {
      assert.equal(
        matchesParameterSearch(parameter(name), 'param*Attribute'),
        true,
      );
    }
    assert.equal(
      matchesParameterSearch(parameter('paramAttribute'), 'param*Attribute'),
      true,
    );
    assert.equal(
      matchesParameterSearch(
        parameter('param01AttributeExtra'),
        'param*Attribute',
      ),
      true,
    );
    assert.equal(
      matchesParameterSearch(
        parameter('_param01Attributes'),
        'param*Attribute',
      ),
      false,
    );
    assert.equal(
      matchesParameterSearch(parameter('_param01Attribute'), '*param*'),
      true,
    );
    assert.equal(
      matchesParameterSearch(parameter('paramDateFormat'), 'param*At'),
      false,
    );
    assert.equal(
      matchesParameterSearch(parameter('param01Attribute'), 'param*At'),
      true,
    );
  });

  it('searches descriptions case-insensitively and allows wildcard matches across lines', () => {
    assert.equal(
      matchesParameterSearch(parameter('other', 'Uses PARAM value'), 'param'),
      true,
    );
    assert.equal(
      matchesParameterSearch(
        parameter('other', 'prefix param\n01 Attribute suffix'),
        'param*Attribute',
      ),
      true,
    );
  });

  it('escapes regular-expression metacharacters and treats blank queries as all', () => {
    assert.equal(
      matchesParameterSearch(parameter('param.01'), 'param.01'),
      true,
    );
    assert.equal(
      matchesParameterSearch(parameter('paramX01'), 'param.01'),
      false,
    );
    assert.equal(matchesParameterSearch(parameter('_param'), '   '), true);
  });
});
