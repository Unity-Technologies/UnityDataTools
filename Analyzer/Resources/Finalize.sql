CREATE INDEX refs_object_index ON refs(object);
CREATE INDEX refs_referenced_object_index ON refs(referenced_object);
CREATE INDEX shader_sp_index ON shader_subprograms(shader);