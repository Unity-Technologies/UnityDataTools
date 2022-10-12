CREATE INDEX refs_object_index ON refs(object);
CREATE INDEX refs_referenced_object_index ON refs(referenced_object);
CREATE INDEX shader_subprograms_shader_index ON shader_subprograms(shader);

-- TODO: Processors should have a Finalize method and this should be moved into the ShaderProcessor.
CREATE INDEX shader_subprogram_keywords_subprogram_id_index ON shader_subprogram_keywords(subprogram_id);
